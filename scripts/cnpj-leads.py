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
  python3 scripts/cnpj-leads.py --uf RJ --cidades "Rio de Janeiro" --limite 10      # teste rapido: so 10 empresas
  python3 scripts/cnpj-leads.py --uf SP --cidades "Campinas,Valinhos" --porte ME,EPP
  python3 scripts/cnpj-leads.py --uf MG --cnae 86,8650 --com-contato
  python3 scripts/cnpj-leads.py --somente-baixar          # so baixa a base do mes (uma vez por mes)

Com --limite ate 300 (modo rapido) baixa so o necessario (centenas de MB) e completa razao social/porte/MEI pela
BrasilAPI (servico aberto que serve os mesmos dados da Receita). Sem limite, baixa a base completa (~7,6 GB, fica em
cache; downloads retomam se cair). So Python padrao, nada para instalar.
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


# ---------------------------------------------------------------- dados da empresa (razao social, porte, MEI)

BRASILAPI = "https://brasilapi.com.br/api/cnpj/v1/"
QUICK_MAX = 300  # ate este --limite, usa o modo rapido (sem baixar Empresas/Simples)
API_PORTE = {1: "01", 3: "03", 5: "05"}


def company_from_api(cnpj: str) -> tuple[str, str, str, bool] | None:
    """(razao social, natureza, porte, MEI) pela BrasilAPI - mesmos dados da Receita, consulta por CNPJ de empresa."""
    for attempt in range(1, 6):
        try:
            req = urllib.request.Request(BRASILAPI + cnpj, headers={"User-Agent": "vitta-leads/1.0", "Accept": "application/json"})
            with urllib.request.urlopen(req, timeout=30) as resp:
                d = __import__("json").loads(resp.read())
            return (d.get("razao_social") or "", str(d.get("codigo_natureza_juridica") or ""),
                    API_PORTE.get(d.get("codigo_porte"), "00"), bool(d.get("opcao_pelo_mei")))
        except urllib.error.HTTPError as exc:
            if exc.code == 404:
                return None
            wait = 15 * attempt if exc.code == 429 else 3 * attempt  # 429 = muitas consultas: espera mais
        except (urllib.error.URLError, TimeoutError, ConnectionError, OSError, ValueError):
            wait = 3 * attempt
        time.sleep(wait)
    return None


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
    ap.add_argument("--limite", type=int, default=0,
                    help=f"Quantidade maxima de empresas (0 = todas). Ate {QUICK_MAX}: modo rapido, baixa so o necessario")
    ap.add_argument("--completo", action="store_true", help="Forca o modo completo (baixa toda a base) mesmo com --limite pequeno")
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
    cache.mkdir(parents=True, exist_ok=True)
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

    estab_files = sorted((n for n in files if re.match(r"Estabelecimentos\d+\.zip$", n)), key=lambda n: files[n])  # menores primeiro
    empresa_files = sorted(n for n in files if re.match(r"Empresas\d+\.zip$", n))
    quick = bool(args.limite) and args.limite <= QUICK_MAX and not args.completo and not args.somente_baixar
    base_needed = ["Municipios.zip", "Cnaes.zip"] + ([] if quick else empresa_files + ["Simples.zip"])
    missing = [n for n in base_needed + estab_files if n not in files]
    if missing:
        sys.exit(f"Arquivos ausentes na pasta {month}: {missing}")

    def get(name: str) -> Path:
        cached = cache / month / name
        if args.offline:
            if not cached.exists():
                sys.exit(f"{name} nao esta no cache (rode sem --offline).")
            return cached
        if not cached.exists():
            free = shutil.disk_usage(cache).free
            if files[name] > free:
                sys.exit(f"Espaco insuficiente para {name}: {files[name] / 1e9:.1f} GB, livres {free / 1e9:.1f} GB em {cache}.")
            log(f"Baixando {name} ({files[name] / 1e6:,.0f} MB)...")
        return receita.download(month, name, files[name])

    if args.somente_baixar:
        total = sum(files[n] for n in base_needed + estab_files if not (cache / month / n).exists())
        log(f"Baixando {total / 1e9:.1f} GB (se cair, rode de novo que continua)...")
        for n in base_needed + estab_files:
            get(n)
        log("Base baixada.")
        return

    if quick:
        log(f"Modo rapido (limite {args.limite}): baixa so os arquivos necessarios e completa os dados pela BrasilAPI.")
    elif not args.offline:
        need = sum(files[n] for n in base_needed + estab_files if not (cache / month / n).exists())
        if need:
            log(f"Modo completo: {need / 1e9:.1f} GB a baixar (so na primeira vez do mes; se cair, rode de novo).")

    ufs = {u.strip().upper() for u in args.uf.split(",") if u.strip()}
    cities = {norm(c) for c in (args.cidades or "").split(",") if c.strip()}
    cnaes = tuple(digits(c) for c in (args.cnae or "").split(",") if digits(c))
    portes = {p.strip().upper() for p in args.porte.split(",")}
    porte_codes = {code for code, name in PORTE.items() if name.upper() in portes}

    municipios = {code: name for code, name in rows(get("Municipios.zip"))}
    wanted_muni = {code for code, name in municipios.items() if norm(name) in cities} if cities else None
    if cities:
        found = {norm(municipios[c]) for c in wanted_muni}
        for c in sorted(cities - found):
            log(f"  Aviso: cidade '{c}' nao existe na tabela da Receita (confira a grafia).")
        if not wanted_muni:
            sys.exit("Nenhuma das cidades informadas existe na tabela da Receita.")
    cnae_desc = {code: desc for code, desc in rows(get("Cnaes.zip"))}

    uf_marks = [f'";"{u}";"' for u in ufs]

    def scan(path: Path, out: dict[str, list[str]], stop_at: int = 0) -> None:
        """Estabelecimentos que passam nos filtros. Para cedo (stop_at) no modo rapido."""
        with zipfile.ZipFile(path) as z:
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
                        out[r[0] + r[1] + r[2]] = r
                        if stop_at and len(out) >= stop_at:
                            return

    def accept(company: tuple[str, str, str, bool]) -> str | None:
        """Motivo de exclusao (ou None = entra)."""
        _, natureza, porte, is_mei = company
        if is_mei and not args.incluir_mei:
            return "mei"
        if natureza == NATUREZA_EMPRESARIO_INDIVIDUAL and not args.incluir_individual:
            return "individual"
        if porte not in porte_codes:
            return "porte"
        return None

    skipped = {"mei": 0, "individual": 0, "porte": 0, "sem dados": 0}
    chosen: list[tuple[str, list[str], tuple[str, str, str, bool]]] = []

    if quick:
        # Le um arquivo por vez (menores primeiro) e completa pela API ate atingir o limite.
        seen: set[str] = set()
        for name in estab_files:
            candidates: dict[str, list[str]] = {}
            log(f"Procurando em {name}...")
            scan(get(name), candidates, stop_at=args.limite * 6)
            new = [k for k in candidates if k not in seen]
            log(f"  {len(new)} candidata(s); consultando razao social, porte e MEI...")
            for key in new:
                seen.add(key)
                company = company_from_api(key)
                time.sleep(0.35)  # gentileza com o servico gratuito
                if company is None:
                    skipped["sem dados"] += 1
                    continue
                reason = accept(company)
                if reason:
                    skipped[reason] += 1
                    continue
                chosen.append((key, candidates[key], company))
                if len(chosen) >= args.limite:
                    break
            if len(chosen) >= args.limite:
                break
    else:
        estab: dict[str, list[str]] = {}
        for name in estab_files:
            before = len(estab)
            log(f"Lendo {name}...")
            scan(get(name), estab)
            log(f"  +{len(estab) - before} (total {len(estab)})")
        bases = {k[:8] for k in estab}
        log("Lendo empresas...")
        empresas: dict[str, tuple[str, str, str]] = {}
        for name in empresa_files:
            for r in rows(get(name)):
                if r and r[0] in bases:
                    empresas[r[0]] = (r[1], r[2], r[5] if len(r) > 5 else "00")
        log("Lendo Simples/MEI...")
        mei = {r[0] for r in rows(get("Simples.zip")) if len(r) > 4 and r[0] in bases and r[4] == "S"}
        for key in sorted(estab, key=lambda k: (estab[k][20], estab[k][4] or empresas.get(k[:8], ("",))[0])):
            razao, natureza, porte = empresas.get(key[:8], ("", "", "00"))
            company = (razao, natureza, porte, key[:8] in mei)
            reason = accept(company)
            if reason:
                skipped[reason] += 1
                continue
            chosen.append((key, estab[key], company))
            if args.limite and len(chosen) >= args.limite:
                break

    # Planilha no formato da importacao do Vitta (cabecalhos que o modelo reconhece sozinho).
    out_path = Path(args.saida or f"leads-{'-'.join(sorted(ufs)).lower()}-{month}.csv")
    origem = f"Receita Federal – Dados Abertos CNPJ {month} (extraído em {time.strftime('%d/%m/%Y')})"
    header = ["Nome", "Razão social", "CNPJ", "Telefone", "Telefone 2", "E-mail", "Endereço", "Cidade", "UF", "CEP",
              "Atividade (CNAE)", "Porte", "Data de abertura", "Origem"]
    with open(out_path, "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f, delimiter=";")
        w.writerow(header)
        for key, r, (razao, _, porte, _) in chosen:
            street = " ".join(p for p in (r[13], r[14]) if p).strip()
            number = r[15].strip().lstrip("0") or r[15].strip()   # a Receita grava "00111"
            address = ", ".join(p for p in (title(street), number) if p)
            extra = " - ".join(title(p) for p in (r[16], r[17]) if p.strip())
            address = f"{address} - {extra}" if extra else address
            cnae = r[11]
            w.writerow([
                title(r[4] or razao), title(razao), fmt_cnpj(key), fmt_phone(r[21], r[22]), fmt_phone(r[23], r[24]),
                r[27].strip().lower(), address, title(municipios.get(r[20], "")), r[19], fmt_cep(r[18]),
                f"{fmt_cnae(cnae)} {cnae_desc.get(cnae, '')}".strip(), PORTE.get(porte, ""), fmt_date(r[10]), origem,
            ])

    log(f"Pronto: {len(chosen)} empresa(s) em {out_path.resolve()}")
    log(f"  origem registrada em cada linha: {origem}")
    log(f"  fora: {skipped['mei']} MEI, {skipped['individual']} empresario individual, {skipped['porte']} por porte"
        + (f", {skipped['sem dados']} sem dados na consulta" if skipped["sem dados"] else "")
        + " (use --incluir-mei / --incluir-individual / --porte para mudar)")
    if args.limite and len(chosen) < args.limite:
        log("  Aviso: encontrou menos que o limite pedido (filtros restritivos ou cidade pequena).")
    log("No Vitta: Modelos de planilha > novo modelo com esta planilha (as colunas sao reconhecidas sozinhas),"
        " depois Prospeccao > Importar planilha.")


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        sys.exit("\nInterrompido. Rode de novo: os downloads continuam de onde pararam.")
