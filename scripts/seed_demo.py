"""
Preenche a API com dados FICTICIOS para demonstracao (clientes, apolices, prospects e duas
importacoes de carteira). Use em um banco de desenvolvimento vazio:

    rm src/Vitta.API/vitta.db      # zera o SQLite local
    npm run api                    # sobe a API (recria o banco)
    python3 scripts/seed_demo.py   # ou: npm run seed:demo
"""

import datetime as dt
import json
import random
import sys
import urllib.error
import urllib.request
import uuid

BASE = (sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5080").rstrip("/") + "/api"
random.seed(42)
TOKEN = None


def call(method, path, body=None, raw=None, content_type="application/json"):
    data = raw if raw is not None else (json.dumps(body).encode() if body is not None else None)
    req = urllib.request.Request(BASE + path, data=data, method=method)
    if data is not None:
        req.add_header("Content-Type", content_type)
    if TOKEN:
        req.add_header("Authorization", f"Bearer {TOKEN}")
    try:
        with urllib.request.urlopen(req) as r:
            text = r.read().decode()
            return json.loads(text) if text else None
    except urllib.error.HTTPError as e:
        sys.exit(f"Falhou {method} {path}: {e.code} {e.read().decode()[:300]}")


def cpf():
    d = [random.randint(0, 9) for _ in range(9)]
    for n in (10, 11):
        s = sum(v * (n - i) for i, v in enumerate(d))
        d.append(0 if s % 11 < 2 else 11 - s % 11)
    return "".join(map(str, d))


def fmt_cpf(c):
    return f"{c[:3]}.{c[3:6]}.{c[6:9]}-{c[9:]}"


def multipart(fields, filename, content):
    boundary = uuid.uuid4().hex
    body = b"".join(
        f'--{boundary}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode() for k, v in fields.items()
    )
    body += (
        f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{filename}"\r\n'
        f"Content-Type: text/csv\r\n\r\n".encode() + content.encode() + f"\r\n--{boundary}--\r\n".encode()
    )
    return body, f"multipart/form-data; boundary={boundary}"


TOKEN = call("POST", "/auth/login", {"email": "admin@vitta.local", "password": "Vitta@2026"})["accessToken"]
if call("GET", "/clients?pageSize=1")["totalCount"] > 0:
    sys.exit("O banco ja tem clientes. Rode em um banco vazio (apague src/Vitta.API/vitta.db).")

insurers = {i["code"]: i for i in call("GET", "/insurers")}
today = dt.date.today()

names = [
    "Ana Beatriz Lima", "Bruno Henrique Reis", "Carla Mendes Prado", "Diego Fonseca", "Eduarda Nogueira",
    "Fábio Augusto Torres", "Gabriela Antunes", "Heitor Campos", "Isabela Rocha", "João Pedro Almeida",
    "Larissa Figueiredo", "Marcelo Duarte", "Natália Barros", "Otávio Siqueira", "Patrícia Moura",
]
products = [("Vida Inteira", 180, 420), ("Vida Temporária", 60, 160), ("Doenças Graves", 90, 240), ("Invalidez", 40, 110)]
weights = [("MAG", 0.6), ("PRUDENTIAL", 0.2), ("ICATU", 0.12), ("METLIFE", 0.08)]

print("Criando clientes e apolices...")
mag_rows = []
policy_no = 500100
for name in names:
    c = cpf()
    client = call("POST", "/clients", {
        "fullName": name, "cpf": c,
        "birthDate": str(dt.date(random.randint(1960, 1995), random.randint(1, 12), random.randint(1, 28))),
        "phone": f"(21) 9{random.randint(8000, 9999)}-{random.randint(1000, 9999)}",
        "email": name.split()[0].lower().replace("á", "a").replace("ã", "a").replace("é", "e").replace("í", "i") + "@exemplo.com",
    })
    for _ in range(random.choice([1, 1, 2])):
        code = random.choices([w[0] for w in weights], [w[1] for w in weights])[0]
        product, lo, hi = random.choice(products)
        premium = round(random.uniform(lo, hi), 2)
        policy_no += random.randint(1, 40)
        call("POST", "/policies", {
            "clientId": client["id"], "insurerId": insurers[code]["id"], "policyNumber": str(policy_no),
            "productName": product, "monthlyPremium": premium,
            "issuedOn": str(today - dt.timedelta(days=random.randint(120, 3000))),
        })
        if code == "MAG":
            mag_rows.append((str(policy_no), name, fmt_cpf(c), product, premium))

print("Criando prospects...")
prospects = [
    ("Rafael Cunha", "Indicação", "New", 2), ("Sabrina Teles", "Instagram", "Contacted", -1),
    ("Thiago Mattos", "Indicação", "MeetingScheduled", 0), ("Vanessa Lopes", "Evento", "ProposalSent", 3),
    ("William Prates", "Site", "New", None), ("Yasmin Ferraz", "Indicação", "Contacted", 5),
    ("Zeca Pimentel", "Evento", "Lost", None), ("Aline Bastos", "Instagram", "ProposalSent", -3),
]
for name, source, stage, days in prospects:
    follow = str(today + dt.timedelta(days=days)) if days is not None else None
    p = call("POST", "/prospects", {"name": name, "source": source, "nextFollowUpOn": follow,
                                     "phone": f"(21) 9{random.randint(8000, 9999)}-{random.randint(1000, 9999)}"})
    if stage != "New":
        call("POST", f"/prospects/{p['id']}/stage", {"stage": stage, "nextFollowUpOn": follow})

print("Importando carteira MAG de dois meses...")
header = "Apólice;Segurado;CPF;Produto;Prêmio Mensal;Situação\n"
m2 = (today.replace(day=1) - dt.timedelta(days=1)).replace(day=1)
m1 = (m2 - dt.timedelta(days=1)).replace(day=1)
leaving = mag_rows[:2]  # simulam saida (troca de corretor / cancelamento)
month1 = header + "".join(f"{n};{h};{c};{p};{v:.2f}".replace(".", ",") + ";Ativa\n" for n, h, c, p, v in mag_rows)
month2_rows = [r for r in mag_rows if r not in leaving]
month2 = header + "".join(
    f"{n};{h};{c};{p};{(v * 1.05 if i == 0 else v):.2f}".replace(".", ",") + ";Ativa\n" for i, (n, h, c, p, v) in enumerate(month2_rows)
) + "509999;Renata Vasconcelos;;Vida Inteira;230,00;Ativa\n"
for month, content in ((m1, month1), (m2, month2)):
    body, ct = multipart({"insurerId": insurers["MAG"]["id"], "referenceMonth": month.strftime("%Y-%m")}, f"mag-{month:%Y-%m}.csv", content)
    call("POST", "/portfolio/imports", raw=body, content_type=ct)

print(f"Pronto! {len(names)} clientes, {len(prospects)} prospects e 2 importacoes da MAG "
      f"({len(leaving)} saidas para investigar).")
