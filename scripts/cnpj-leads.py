#!/usr/bin/env python3
"""
Buscador de leads de EMPRESAS a partir dos dados abertos do CNPJ (Receita Federal).

Gera uma planilha pronta para a importacao de prospeccao do Vitta (Prospeccao > Importar planilha), por exemplo
para oferecer seguro de vida em grupo. Fonte publica e oficial, feita para reuso; mesmo assim:
  - so empresas ATIVAS;
  - MEI e empresario individual ficam de FORA por padrao (o contato deles e, na pratica, dado de pessoa fisica);
  - socios NAO sao baixados nem usados (minimo de dados pessoais);
  - a coluna Origem registra a fonte e o mes da base (fica no lote de importacao).

Uso (exemplos):
  python3 scripts/cnpj-leads.py --uf SP --cidades "Campinas,Valinhos" --porte ME,EPP
  python3 scripts/cnpj-leads.py --uf MG --cnae 86,8650 --com-contato
  python3 scripts/cnpj-leads.py --somente-baixar          # so baixa a base do mes (uma vez por mes)

A base completa tem ~7,6 GB (fica em cache; downloads retomam se cair). So Python padrao, nada para instalar.
"""

from __future__ import annotations

import argparse
import base64
import csv
import io
import os
import re
import shutil
import sys
import time
import unicodedata
import urllib.error
import urllib.request
import zipfile
from pathlib import Path

# Compartilhamento publico (Nextcloud) da Receita com as pastas mensais. A Receita ja trocou esse endereco mais de
# uma vez; se parar de funcionar, passe outro com --token.
WEBDAV = "https://arquivos.receitafederal.gov.br/public.php/webdav/"
DEFAULT_TOKEN = "YggdBLfdninEJX9"

PORTE = {"00": "Não informado", "01": "ME", "03": "EPP", "05": "Demais"}
NATUREZA_EMPRESARIO_INDIVIDUAL = "2135"
SITUACAO_ATIVA = "02"
MATRIZ = "1"

csv.field_size_limit(10_000_000)


# ---------------------------------------------------------------- utilidades

def log(msg: str) -> None:
    print(f"[{time.strftime('%H:%M:%S')}] {msg}", flush=True)


def norm(text: str) -> str:
    """'São José dos Campos' -> 'SAO JOSE DOS CAMPOS' (a Receita grava municipios sem acento, em maiusculas)."""
    text = unicodedata.normalize("NFKD", text).encode("ascii", "ignore").decode()
    return re.sub(r"\s+", " ", text).strip().upper()


def title(text: str) -> str:
    """'BOM SUPERMERCADOS S.A.' -> 'Bom Supermercados S.A.' (siglas ficam em maiusculas)."""
    small = {"de", "da", "do", "das", "dos", "e"}
    acronyms = {"me", "epp", "eireli", "ltda-me", "sa", "s/a", "s.a", "s.a.", "cia", "ss"}

    def fix(i: int, w: str) -> str:
        if w in acronyms or (len(w) <= 5 and "." in w.strip(".")):
            return "Ltda-ME" if w == "ltda-me" else ("Cia" if w == "cia" else w.upper())
        return w if (i and w in small) else w.capitalize()

    return " ".join(fix(i, w) for i, w in enumerate(text.strip().lower().split()))


def digits(text: str) -> str:
    return re.sub(r"\D", "", text or "")


def fmt_phone(ddd: str, number: str) -> str:
    ddd, number = digits(ddd), digits(number)
    if not number:
        return ""
    if len(number) >= 8:
        number = f"{number[:-4]}-{number[-4:]}"
    return f"({ddd}) {number}" if ddd else number


def fmt_cnpj(c: str) -> str:
    return f"{c[:2]}.{c[2:5]}.{c[5:8]}/{c[8:12]}-{c[12:]}"


def fmt_cep(c: str) -> str:
    c = digits(c)
    return f"{c[:5]}-{c[5:]}" if len(c) == 8 else c


def fmt_date(yyyymmdd: str) -> str:
    d = digits(yyyymmdd)
    return f"{d[6:8]}/{d[4:6]}/{d[:4]}" if len(d) == 8 and d != "00000000" else ""


def fmt_cnae(code: str) -> str:
    c = digits(code).rjust(7, "0")
    return f"{c[:4]}-{c[4]}/{c[5:]}"


# ---------------------------------------------------------------- download

class Receita:
    def __init__(self, token: str, cache: Path):
        self.auth = "Basic " + base64.b64encode(f"{token}:".encode()).decode()
        self.cache = cache

    def _request(self, path: str, method: str = "GET", headers: dict | None = None) -> urllib.request.Request:
        req = urllib.request.Request(WEBDAV + path, method=method, headers={"Authorization": self.auth, **(headers or {})})
        return req

    def listing(self, folder: str = "") -> dict[str, int]:
        """Nome -> tamanho (bytes) dos itens de uma pasta."""
        req = self._request(folder, "PROPFIND", {"Depth": "1"})
        xml = urllib.request.urlopen(req, timeout=60).read().decode("utf-8", "replace")
        items = {}
        for block in re.findall(r"<d:response>.*?</d:response>", xml, re.S):
            href = re.search(r"<d:href>([^<]+)</d:href>", block).group(1).rstrip("/").split("/")[-1]
            size = re.search(r"<d:getcontentlength>(\d+)</d:getcontentlength>", block)
            if href and href != "webdav":
                items[href] = int(size.group(1)) if size else -1
        return items

    def latest_month(self) -> str:
        months = sorted(n for n in self.listing() if re.fullmatch(r"\d{4}-\d{2}", n))
        if not months:
            sys.exit("Nao encontrei pastas mensais na Receita. O endereco pode ter mudado (use --token).")
        return months[-1]

    def download(self, month: str, name: str, size: int) -> Path:
        """Baixa com retomada (Range). Se o arquivo ja estiver completo no cache, nao baixa de novo."""
        target = self.cache / month / name
        target.parent.mkdir(parents=True, exist_ok=True)
        part = target.with_suffix(target.suffix + ".part")
        if target.exists() and (size < 0 or target.stat().st_size == size):
            return target
        for attempt in range(1, 9):
            have = part.stat().st_size if part.exists() else 0
            if size >= 0 and have >= size:
                break
            headers = {"Range": f"bytes={have}-"} if have else {}
            try:
                with urllib.request.urlopen(self._request(f"{month}/{name}", headers=headers), timeout=120) as resp, \
                        open(part, "ab" if have else "wb") as out:
                    last = time.time()
                    while chunk := resp.read(1 << 20):
                        out.write(chunk)
                        if time.time() - last > 5:
                            done = out.tell()
                            pct = f"{done * 100 / size:5.1f}%" if size > 0 else f"{done / 1e6:,.0f} MB"
                            log(f"  {name}: {pct}")
                            last = time.time()
                break
            except (urllib.error.URLError, TimeoutError, ConnectionError, OSError) as exc:
                log(f"  {name}: falha ({exc}); tentativa {attempt} de 8, retomando em {attempt * 10}s...")
                time.sleep(attempt * 10)
        else:
            sys.exit(f"Nao consegui baixar {name}. Rode de novo mais tarde (o download continua de onde parou).")
        part.replace(target)
        return target


def rows(zip_path: Path):
    """Linhas (listas) de todos os CSVs dentro do zip. Layout da Receita: ';', aspas, latin-1, sem cabecalho."""
    with zipfile.ZipFile(zip_path) as z:
        for member in z.namelist():
            with z.open(member) as raw:
                yield from csv.reader(io.TextIOWrapper(raw, encoding="latin-1", newline=""), delimiter=";", quotechar='"')


# ---------------------------------------------------------------- processamento

def main() -> None:
    ap = argparse.ArgumentParser(description="Leads de empresas (dados abertos do CNPJ) para o Vitta.")
    ap.add_argument("--uf", help="UF(s), separadas por virgula. Ex.: SP ou SP,MG")
    ap.add_argument("--cidades", help='Cidades, separadas por virgula. Ex.: "Campinas,Valinhos" (sem acento tambem vale)')
    ap.add_argument("--cnae", help="Atividades: inicio do codigo CNAE, separados por virgula. Ex.: 86 (saude), 6201, 4711")
    ap.add_argument("--porte", default="ME,EPP,Demais", help="Portes: ME, EPP, Demais (padrao: todos)")
    ap.add_argument("--com-contato", action="store_true", help="So empresas com telefone ou e-mail")
    ap.add_argument("--incluir-filiais", action="store_true", help="Inclui filiais (padrao: so matriz, onde costuma estar quem decide)")
    ap.add_argument("--incluir-mei", action="store_true", help="Inclui MEI (contato e, na pratica, de pessoa fisica - cuidado com a LGPD)")
    ap.add_argument("--incluir-individual", action="store_true", help="Inclui empresario individual (mesmo cuidado)")
    ap.add_argument("--aberta-ate", type=int, help="So empresas abertas ate este ano (ex.: 2023 = pelo menos ~3 anos de vida)")
    ap.add_argument("--limite", type=int, default=0, help="Maximo de linhas na planilha (0 = sem limite)")
    ap.add_argument("--mes", help="Mes da base (AAAA-MM). Padrao: o mais recente")
    ap.add_argument("--cache", default=str(Path.home() / "Downloads" / "receita-cnpj"), help="Pasta dos arquivos baixados")
    ap.add_argument("--saida", help="Arquivo CSV de saida (padrao: leads-<uf>-<mes>.csv na pasta atual)")
    ap.add_argument("--token", default=DEFAULT_TOKEN, help="Token do compartilhamento publico da Receita (se mudar)")
    ap.add_argument("--somente-baixar", action="store_true", help="So baixa/atualiza a base do mes e sai")
    ap.add_argument("--offline", action="store_true", help="Usa so o que ja esta no cache (sem consultar a Receita)")
    args = ap.parse_args()

    if not args.somente_baixar and not args.uf:
        ap.error("informe --uf (ou use --somente-baixar)")

    cache = Path(args.cache).expanduser()
    receita = Receita(args.token, cache)
    if args.offline:
        months = sorted(p.name for p in cache.glob("????-??") if p.is_dir())
        month = args.mes or (months[-1] if months else "")
        if not month:
            sys.exit(f"Nada no cache {cache}. Rode sem --offline para baixar.")
        files = {p.name: p.stat().st_size for p in (cache / month).glob("*.zip")}
    else:
        month = args.mes or receita.latest_month()
        files = receita.listing(month)
    log(f"Base da Receita: {month} (cache: {cache / month})")

    wanted = sorted(n for n in files if re.match(r"(Estabelecimentos|Empresas)\d+\.zip$", n)) + ["Simples.zip", "Municipios.zip", "Cnaes.zip"]
    missing = [n for n in wanted if n not in files]
    if missing:
        sys.exit(f"Arquivos ausentes na pasta {month}: {missing}")

    need = sum(files[n] for n in wanted if not (cache / month / n).exists())
    cache.mkdir(parents=True, exist_ok=True)
    free = shutil.disk_usage(cache).free
    if need > free:
        sys.exit(f"Espaco insuficiente: faltam baixar {need / 1e9:.1f} GB e ha {free / 1e9:.1f} GB livres em {cache}.")
    if need:
        log(f"Baixando {need / 1e9:.1f} GB (so na primeira vez do mes; se cair, rode de novo que continua)...")
    paths = {n: receita.download(month, n, files[n]) for n in wanted}
    if args.somente_baixar:
        log("Base baixada.")
        return

    ufs = {u.strip().upper() for u in args.uf.split(",") if u.strip()}
    cities = {norm(c) for c in (args.cidades or "").split(",") if c.strip()}
    cnaes = tuple(digits(c) for c in (args.cnae or "").split(",") if digits(c))
    portes = {p.strip().upper() for p in args.porte.split(",")}
    porte_codes = {code for code, name in PORTE.items() if name.upper() in portes}

    # Tabelas auxiliares
    municipios = {code: name for code, name in rows(paths["Municipios.zip"])}
    wanted_muni = {code for code, name in municipios.items() if norm(name) in cities} if cities else None
    if cities:
        found = {norm(municipios[c]) for c in wanted_muni}
        for c in sorted(cities - found):
            log(f"  Aviso: cidade '{c}' nao existe na tabela da Receita (confira a grafia).")
    cnae_desc = {code: desc for code, desc in rows(paths["Cnaes.zip"])}

    # 1) Estabelecimentos: filtra por situacao, UF, cidade, CNAE, matriz e contato.
    log("Lendo estabelecimentos (alguns minutos)...")
    uf_marks = [f'";"{u}";"' for u in ufs]
    estab: dict[str, list[str]] = {}
    for name in sorted(n for n in paths if n.startswith("Estabelecimentos")):
        before = len(estab)
        with zipfile.ZipFile(paths[name]) as z:
            for member in z.namelist():
                with z.open(member) as raw:
                    for line in io.TextIOWrapper(raw, encoding="latin-1", newline=""):
                        if not any(m in line for m in uf_marks):     # filtro rapido antes de interpretar o CSV
                            continue
                        r = next(csv.reader([line], delimiter=";", quotechar='"'))
                        if len(r) < 28 or r[5] != SITUACAO_ATIVA or r[19] not in ufs:
                            continue
                        if not args.incluir_filiais and r[3] != MATRIZ:
                            continue
                        if wanted_muni is not None and r[20] not in wanted_muni:
                            continue
                        if cnaes and not r[11].startswith(cnaes):
                            continue
                        if args.aberta_ate and r[10][:4].isdigit() and int(r[10][:4]) > args.aberta_ate:
                            continue
                        if args.com_contato and not (digits(r[22]) or digits(r[24]) or r[27].strip()):
                            continue
                        estab[r[0] + r[1] + r[2]] = r
        log(f"  {name}: +{len(estab) - before} (total {len(estab)})")
    bases = {k[:8] for k in estab}

    # 2) Empresas: razao social, natureza juridica e porte.
    log("Lendo empresas...")
    empresas: dict[str, tuple[str, str, str]] = {}
    for name in sorted(n for n in paths if n.startswith("Empresas")):
        for r in rows(paths[name]):
            if r and r[0] in bases:
                empresas[r[0]] = (r[1], r[2], r[5] if len(r) > 5 else "00")

    # 3) Simples: quem e MEI.
    log("Lendo Simples/MEI...")
    mei = {r[0] for r in rows(paths["Simples.zip"]) if len(r) > 4 and r[0] in bases and r[4] == "S"}

    # 4) Planilha no formato da importacao do Vitta (cabecalhos que o modelo reconhece sozinho).
    out_path = Path(args.saida or f"leads-{'-'.join(sorted(ufs)).lower()}-{month}.csv")
    origem = f"Receita Federal — dados abertos CNPJ {month}"
    header = ["Nome", "Razão social", "CNPJ", "Telefone", "Telefone 2", "E-mail", "Endereço", "Cidade", "UF", "CEP",
              "Atividade (CNAE)", "Porte", "Data de abertura", "Origem"]
    written = skipped_mei = skipped_ind = skipped_porte = 0
    with open(out_path, "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f, delimiter=";")
        w.writerow(header)
        for key in sorted(estab, key=lambda k: (estab[k][20], estab[k][4] or empresas.get(k[:8], ("",))[0])):
            r = estab[key]
            razao, natureza, porte = empresas.get(key[:8], ("", "", "00"))
            if key[:8] in mei and not args.incluir_mei:
                skipped_mei += 1
                continue
            if natureza == NATUREZA_EMPRESARIO_INDIVIDUAL and not args.incluir_individual:
                skipped_ind += 1
                continue
            if porte not in porte_codes:
                skipped_porte += 1
                continue
            street = " ".join(p for p in (r[13], r[14]) if p).strip()
            address = ", ".join(p for p in (title(street), r[15].strip()) if p)
            extra = " - ".join(title(p) for p in (r[16], r[17]) if p.strip())
            address = f"{address} - {extra}" if extra else address
            cnae = r[11]
            w.writerow([
                title(r[4] or razao), title(razao), fmt_cnpj(key), fmt_phone(r[21], r[22]), fmt_phone(r[23], r[24]),
                r[27].strip().lower(), address, title(municipios.get(r[20], "")), r[19], fmt_cep(r[18]),
                f"{fmt_cnae(cnae)} {cnae_desc.get(cnae, '')}".strip(), PORTE.get(porte, ""), fmt_date(r[10]), origem,
            ])
            written += 1
            if args.limite and written >= args.limite:
                break

    log(f"Pronto: {written} empresa(s) em {out_path.resolve()}")
    log(f"  fora: {skipped_mei} MEI, {skipped_ind} empresario individual, {skipped_porte} por porte"
        " (use --incluir-mei / --incluir-individual / --porte para mudar)")
    log("No Vitta: Modelos de planilha > novo modelo com esta planilha (as colunas sao reconhecidas sozinhas),"
        " depois Prospeccao > Importar planilha.")


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        sys.exit("\nInterrompido. Rode de novo: os downloads continuam de onde pararam.")
