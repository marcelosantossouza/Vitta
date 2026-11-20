"""
Teste de fumaca ponta a ponta da Vitta.API (so biblioteca padrao).

Uso (com a API rodando):  python3 scripts/smoke_test.py [http://localhost:5080]

Cria dados com sufixo aleatorio para poder rodar varias vezes no mesmo banco.
ATENCAO: grava dados de teste no banco apontado. Use apenas em desenvolvimento.
"""

import json
import random
import sys
import urllib.error
import urllib.request
import uuid

BASE = (sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5080").rstrip("/") + "/api"
TOKEN = None
FAILURES = []


def call(method, path, body=None, expect=200, raw=None, content_type="application/json"):
    data = raw if raw is not None else (json.dumps(body).encode() if body is not None else None)
    req = urllib.request.Request(BASE + path, data=data, method=method)
    if data is not None:
        req.add_header("Content-Type", content_type)
    if TOKEN:
        req.add_header("Authorization", f"Bearer {TOKEN}")
    try:
        with urllib.request.urlopen(req) as r:
            status, text = r.status, r.read().decode()
    except urllib.error.HTTPError as e:
        status, text = e.code, e.read().decode()
    ok = status == expect
    print(f"  {'OK ' if ok else 'ERR'} {method:6} {path:45} -> {status}" + ("" if ok else f" (esperado {expect}) {text[:200]}"))
    if not ok:
        FAILURES.append(f"{method} {path}")
    return json.loads(text) if text and text[0] in "[{" else None


def check(cond, msg):
    print(f"  {'OK ' if cond else 'ERR'} {msg}")
    if not cond:
        FAILURES.append(msg)


def cpf():
    """Gera CPF valido aleatorio."""
    d = [random.randint(0, 9) for _ in range(9)]
    for n in (10, 11):
        s = sum(v * (n - i) for i, v in enumerate(d))
        d.append(0 if s % 11 < 2 else 11 - s % 11)
    return "".join(map(str, d))


def multipart(fields, filename, content):
    boundary = uuid.uuid4().hex
    parts = []
    for k, v in fields.items():
        parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode())
    parts.append(
        f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{filename}"\r\n'
        f"Content-Type: text/csv\r\n\r\n".encode() + content.encode("utf-8") + b"\r\n"
    )
    parts.append(f"--{boundary}--\r\n".encode())
    return b"".join(parts), f"multipart/form-data; boundary={boundary}"


print(f"== Vitta smoke test em {BASE}")

print("-- Auth")
call("GET", "/dashboard", expect=401)
call("POST", "/auth/login", {"email": "admin@vitta.local", "password": "errada"}, expect=401)
login = call("POST", "/auth/login", {"email": "ADMIN@vitta.local", "password": "Vitta@2026"})
TOKEN = login["accessToken"]
me = call("GET", "/auth/me")
check(me["email"] == "admin@vitta.local", "me devolve o usuario logado")

print("-- Seguradoras")
insurers = call("GET", "/insurers")
mag = next(i for i in insurers if i["code"] == "MAG")
code = f"T{random.randint(1000, 9999)}"
call("POST", "/insurers", {"code": code, "name": "Seguradora Teste"}, expect=201)
call("POST", "/insurers", {"code": code, "name": "Duplicada"}, expect=409)

print("-- Clientes")
c1, c2 = cpf(), cpf()
client = call("POST", "/clients", {"fullName": "Júlia Conceição", "cpf": c1, "phone": "(21) 99999-0000"}, expect=201)
call("POST", "/clients", {"fullName": "Duplicada", "cpf": c1}, expect=409)
call("POST", "/clients", {"fullName": "", "cpf": "123"}, expect=400)
call("PUT", f"/clients/{client['id']}", {"fullName": "Júlia Conceição Lima", "email": "julia@exemplo.com", "notes": "Prefere WhatsApp"}, expect=204)
other = call("POST", "/clients", {"fullName": "Temporario", "cpf": c2}, expect=201)
call("DELETE", f"/clients/{other['id']}", expect=204)
found = call("GET", "/clients?search=julia%20conceicao")
check(any(i["id"] == client["id"] for i in found["items"]), "busca sem acento encontra o cliente")

print("-- Apolices")
num = str(random.randint(100000, 999999))
policy = call("POST", "/policies", {"clientId": client["id"], "insurerId": mag["id"], "policyNumber": num,
                                     "productName": "Vida Inteira", "monthlyPremium": 150.75, "issuedOn": "2025-03-10"}, expect=201)
call("POST", "/policies", {"clientId": client["id"], "insurerId": mag["id"], "policyNumber": num,
                           "productName": "X", "monthlyPremium": 1, "issuedOn": "2025-03-10"}, expect=409)
call("PUT", f"/policies/{policy['id']}", {"productName": "Vida Inteira Plus", "monthlyPremium": 180.0}, expect=204)
detail = call("GET", f"/clients/{client['id']}")
check(detail["policies"][0]["monthlyPremium"] == 180.0 and detail["policies"][0]["status"] == "Active", "detalhe traz apolice atualizada")
call("DELETE", f"/clients/{client['id']}", expect=409)
listed = call("GET", f"/policies?status=Active&insurerId={mag['id']}&search={num}")
check(listed["totalCount"] == 1, "filtro de apolices por status/seguradora/numero")
call("POST", f"/policies/{policy['id']}/end", {"status": "BrokerChanged", "endedOn": "2026-10-31", "reason": "Outro corretor"}, expect=204)
call("POST", f"/policies/{policy['id']}/end", {"status": "Cancelled", "endedOn": "2026-10-31"}, expect=409)
call("POST", f"/policies/{policy['id']}/reactivate", expect=204)

print("-- Prospeccao")
p = call("POST", "/prospects", {"name": "Marcos Andrade", "phone": "(11) 98888-1111", "source": "Indicação", "nextFollowUpOn": "2020-01-01"}, expect=201)
call("PUT", f"/prospects/{p['id']}", {"name": "Marcos Andrade", "phone": "(11) 98888-1111", "source": "Indicação",
                                       "notes": "Tem 2 filhos", "nextFollowUpOn": "2020-01-01"}, expect=204)
call("POST", f"/prospects/{p['id']}/stage", {"stage": "MeetingScheduled", "nextFollowUpOn": "2020-01-02"}, expect=204)
dash = call("GET", "/dashboard")
check(any(f["id"] == p["id"] for f in dash["followUpsDue"]), "follow-up vencido aparece no painel")
conv = call("POST", f"/prospects/{p['id']}/convert", {"cpf": cpf()}, expect=201)
call("POST", f"/prospects/{p['id']}/convert", {"cpf": cpf()}, expect=409)
won = call("GET", "/prospects?stage=Won")
check(any(x["id"] == p["id"] and x["convertedClientId"] == conv["clientId"] for x in won), "prospect convertido fica como Ganho")
tmp = call("POST", "/prospects", {"name": "Apagar"}, expect=201)
call("DELETE", f"/prospects/{tmp['id']}", expect=204)

print("-- Carteira")
test_insurer = next(i for i in call("GET", "/insurers") if i["code"] == code)
header = "Apólice;Segurado;CPF;Produto;Prêmio;Situação\n"
month1 = header + f"001;Ana Lima;{c1};Vida;100,00;Ativa\n002;Bruno Reis;;Vida;50,00;Ativa\n003;Caio Prado;;Vida;80,00;Ativa\n"
month2 = header + f"1;Ana Lima;{c1};Vida;120,00;Ativa\n003;Caio Prado;;Vida;80,00;Ativa\n004;Dora Melo;;Vida;60,00;Ativa\n"
body, ct = multipart({"insurerId": test_insurer["id"], "referenceMonth": "2026-09"}, "set.csv", month1)
first = call("POST", "/portfolio/imports", raw=body, content_type=ct, expect=201)
check(first["isFirstImport"] and first["totalRows"] == 3 and not first["changes"], "primeira importacao e a base (sem mudancas)")
body, ct = multipart({"insurerId": test_insurer["id"], "referenceMonth": "2026-09"}, "set.csv", month1)
call("POST", "/portfolio/imports", raw=body, content_type=ct, expect=409)
body, ct = multipart({"insurerId": test_insurer["id"], "referenceMonth": "2026-10"}, "out.csv", month2)
second = call("POST", "/portfolio/imports", raw=body, content_type=ct, expect=201)
check((second["entered"], second["left"], second["changed"]) == (1, 1, 1), "out: 1 entrou, 1 saiu, 1 mudou")
ana = next(c for c in second["changes"] if c["holderName"] == "Ana Lima")
check(ana["clientId"] == client["id"], "mudanca relacionada ao cliente pelo CPF")
body, ct = multipart({"insurerId": test_insurer["id"], "referenceMonth": "2026-11"}, "ruim.csv", "Foo;Bar\n1;2\n")
call("POST", "/portfolio/imports", raw=body, content_type=ct, expect=400)
imports = call("GET", "/portfolio/imports")
check(any(i["id"] == second["id"] for i in imports), "historico lista as importacoes")
call("GET", f"/portfolio/imports/{second['id']}")

print("-- Modelos de planilha (IA)")
fields = call("GET", "/import-templates/fields?kind=Portfolio")
check([f["field"] for f in fields if f["required"]] == ["policyNumber", "holderName"], "campos obrigatorios da carteira")
mag_layout = "Relatório de Carteira;;;\nNº Proposta;Nome do Segurado;CPF/CNPJ;Situação\n"
body, ct = multipart({"kind": "Portfolio"}, "mag.csv", mag_layout + f"001;Ana Lima;{c1};ATIVO\n")
analysis = call("POST", "/import-templates/analyze", raw=body, content_type=ct)
check(analysis["headerRow"] == 2 and {"field": "policyNumber", "column": "Nº Proposta"} in analysis["columns"],
      f"analise acha o cabecalho na linha 2 ({analysis['analyzedBy']})")
tpl_name = f"Carteira teste {code}"
tpl = call("POST", "/import-templates", {"name": tpl_name, "kind": "Portfolio", "insurerId": test_insurer["id"],
                                         "headerRow": analysis["headerRow"], "columns": analysis["columns"],
                                         "createdByAi": analysis["analyzedBy"] == "ai", "aiNotes": analysis["notes"]}, expect=201)
call("POST", "/import-templates", {"name": tpl_name.upper(), "kind": "Portfolio", "headerRow": 1,
                                   "columns": analysis["columns"]}, expect=409)
call("POST", "/import-templates", {"name": "Sem segurado", "kind": "Portfolio", "headerRow": 1,
                                   "columns": [{"field": "policyNumber", "column": "A"}]}, expect=400)
body, ct = multipart({"referenceMonth": "2026-11", "templateId": tpl["id"]}, "mag-nov.csv",
                     mag_layout + f"001;Ana Lima;{c1};ATIVO\n003;Caio Prado;;CANCELADO\n")
nov = call("POST", "/portfolio/imports", raw=body, content_type=ct, expect=201)
check(nov["insurerId"] == test_insurer["id"] and nov["totalRows"] == 2, "importacao com modelo usa a seguradora do modelo")
body, ct = multipart({"referenceMonth": "2026-12", "templateId": tpl["id"]}, "mudou.csv", "Apólice;Nome\n1;X\n")
call("POST", "/portfolio/imports", raw=body, content_type=ct, expect=400)
listed = call("GET", "/import-templates?kind=Portfolio")
check(any(t["id"] == tpl["id"] and t["lastUsedAt"] for t in listed), "modelo registra o ultimo uso")

ptpl = call("POST", "/import-templates", {"name": f"Contatos {code}", "kind": "Prospects", "headerRow": 1,
                                          "columns": [{"field": "name", "column": "Nome"}, {"field": "phone", "column": "Celular"},
                                                      {"field": "notes", "column": "Profissão"}]}, expect=201)
phone = f"(21) 9{random.randint(1000, 9999)}-{random.randint(1000, 9999)}"
body, ct = multipart({"templateId": ptpl["id"]}, "contatos.csv",
                     f"Nome;Celular;Profissão\nLead {code};{phone};Médica\nLead repetido;{phone};\n;;\n")
res = call("POST", "/prospects/imports", raw=body, content_type=ct)
check((res["created"], res["skipped"]) == (1, 1), "importa prospects e pula telefone repetido")
call("DELETE", f"/portfolio/imports/{nov['id']}", expect=204)
call("DELETE", f"/import-templates/{ptpl['id']}", expect=204)
call("DELETE", f"/import-templates/{tpl['id']}", expect=204)
call("GET", f"/import-templates/{tpl['id']}", expect=404)

print("-- Painel")
dash = call("GET", "/dashboard")
check(dash["activePolicies"] >= 1 and dash["monthlyPremiumTotal"] >= 180, "painel soma apolices ativas")
check(any(e["holderName"] == "Bruno Reis" for e in dash["recentExits"]), "saida aparece no painel")

print("-- Limpeza da carteira de teste")
call("DELETE", f"/portfolio/imports/{second['id']}", expect=204)
call("DELETE", f"/portfolio/imports/{first['id']}", expect=204)

print()
if FAILURES:
    print(f"FALHOU ({len(FAILURES)}):", *FAILURES, sep="\n  - ")
    sys.exit(1)
print("Tudo certo!")
