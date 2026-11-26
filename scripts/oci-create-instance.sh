#!/usr/bin/env bash
# Cria a maquina Always Free do Vitta na Oracle Cloud, tentando de novo enquanto faltar capacidade.
#
# Faz o mesmo que o formulario do console (A1.Flex 1 OCPU / 4 GB, Ubuntu 24.04, IP publico), tentando os
# availability domains em rodizio a cada INTERVALO minutos - a orientacao da Oracle para "Out of host capacity"
# e justamente tentar outro AD ou mais tarde. Para sozinho quando consegue ou quando aparece um erro diferente.
#
# Pre-requisitos: OCI CLI instalado e configurado (oci setup config) - ver docs no fim deste arquivo.
#
# Uso:
#   SUBNET_ID=ocid1.subnet... SSH_KEY=~/Documents/vitta/ssh-key.key.pub ./scripts/oci-create-instance.sh
# Variaveis opcionais: OCPUS (1), MEMORY_GB (4), INTERVALO (15, minimo 5), NOME (vitta), DRY_RUN=1 (so mostra o comando)
#   SHAPE (VM.Standard.A1.Flex) - para a maquina AMD grátis de 1 GB use SHAPE=VM.Standard.E2.1.Micro
#     (ai o padrao vira: imagem Ubuntu 24.04 Minimal x86, nome 'vitta-micro', so no AD onde a Micro grátis existe)
#   SO_VERSAO (24.04, ou "24.04 Minimal" na Micro), SO_AD (trecho do nome do AD para restringir, ex.: AD-3)
#
# Rodando DENTRO de uma maquina da Oracle (sem copiar a chave da sua conta para la):
#   OCI_CLI_AUTH=instance_principal TENANCY_ID=ocid1.tenancy... SUBNET_ID=... SSH_KEY=... ./scripts/oci-create-instance.sh
#   (exige o dynamic group + policy descritos no guia de deploy)
set -euo pipefail

: "${SUBNET_ID:?defina SUBNET_ID (OCID da public subnet-vitta-vcn)}"
: "${SSH_KEY:?defina SSH_KEY (arquivo .pub da chave SSH)}"
SHAPE="${SHAPE:-VM.Standard.A1.Flex}"
OCPUS="${OCPUS:-1}"
MEMORY_GB="${MEMORY_GB:-4}"
INTERVALO="${INTERVALO:-15}"
if [ "$SHAPE" = "VM.Standard.E2.1.Micro" ]; then
  NOME="${NOME:-vitta-micro}"
  SO_VERSAO="${SO_VERSAO:-24.04 Minimal}"
  SO_AD="${SO_AD:-AD-3}"
else
  NOME="${NOME:-vitta}"
  SO_VERSAO="${SO_VERSAO:-24.04}"
  SO_AD="${SO_AD:-}"
fi
FLEX=0; case "$SHAPE" in *.Flex) FLEX=1;; esac
SSH_KEY="${SSH_KEY/#\~/$HOME}"

[ "$INTERVALO" -ge 5 ] || { echo "INTERVALO minimo e 5 minutos (nao vamos martelar a API da Oracle)."; exit 1; }
[ -f "$SSH_KEY" ] || { echo "Arquivo de chave publica nao encontrado: $SSH_KEY"; exit 1; }
command -v oci >/dev/null || { echo "OCI CLI nao encontrado. Instale com: brew install oci-cli"; exit 1; }

log() { echo "[$(date '+%d/%m %H:%M:%S')] $*"; }

# A subnet diz o compartment; o tenancy (raiz) vem da configuracao do CLI.
COMPARTMENT_ID=$(oci network subnet get --subnet-id "$SUBNET_ID" --query 'data."compartment-id"' --raw-output)
TENANCY_ID="${TENANCY_ID:-$(awk -F= '/^tenancy/ {gsub(/ /,"",$2); print $2; exit}' "${OCI_CLI_CONFIG_FILE:-$HOME/.oci/config}" 2>/dev/null || true)}"
[ -n "$TENANCY_ID" ] || { echo "Defina TENANCY_ID (ocid1.tenancy...) ou configure o OCI CLI (oci setup config)."; exit 1; }

# (sem mapfile: o bash que vem no macOS e o 3.2)
ADS=()
while IFS= read -r ad; do [ -n "$ad" ] && [[ "$ad" == *"$SO_AD"* ]] && ADS+=("$ad"); done < <(
  oci iam availability-domain list --compartment-id "$TENANCY_ID" --query 'data[].name' --raw-output | tr ',' '\n' | tr -d '[]" ')
[ "${#ADS[@]}" -gt 0 ] || { echo "Nao consegui listar os availability domains."; exit 1; }

IMAGE_ID=$(oci compute image list --compartment-id "$COMPARTMENT_ID" \
  --operating-system "Canonical Ubuntu" --operating-system-version "$SO_VERSAO" --shape "$SHAPE" \
  --sort-by TIMECREATED --sort-order DESC --limit 1 --query 'data[0].id' --raw-output)
[ -n "$IMAGE_ID" ] && [ "$IMAGE_ID" != "null" ] || { echo "Nao achei a imagem Ubuntu $SO_VERSAO para $SHAPE."; exit 1; }

log "ADs: ${ADS[*]}"
log "Imagem: $IMAGE_ID"
if [ "$FLEX" = 1 ]; then TAM="${OCPUS} OCPU / ${MEMORY_GB} GB"; else TAM="tamanho fixo"; fi
log "Maquina: $SHAPE ($TAM), Ubuntu $SO_VERSAO, nome '$NOME'. Tentando a cada ${INTERVALO} min. Ctrl+C para parar."

tentativa=0
while true; do
  for AD in "${ADS[@]}"; do
    tentativa=$((tentativa + 1))
    CMD=(oci compute instance launch
      --availability-domain "$AD"
      --compartment-id "$COMPARTMENT_ID"
      --display-name "$NOME"
      --shape "$SHAPE"
      --image-id "$IMAGE_ID"
      --subnet-id "$SUBNET_ID"
      --assign-public-ip true
      --is-pv-encryption-in-transit-enabled true
      --ssh-authorized-keys-file "$SSH_KEY")
    if [ "$FLEX" = 1 ]; then CMD+=(--shape-config "{\"ocpus\": $OCPUS, \"memoryInGBs\": $MEMORY_GB}"); fi

    if [ "${DRY_RUN:-0}" = "1" ]; then
      printf '%q ' "${CMD[@]}"; echo; exit 0
    fi

    log "Tentativa $tentativa em $AD..."
    if SAIDA=$("${CMD[@]}" 2>&1); then
      log "CONSEGUIU! Maquina criada em $AD."
      echo "$SAIDA" | grep -E '"(id|lifecycle-state|display-name)"' | head -3
      log "Abra o console (Compute > Instances) e aguarde ficar Running para ver o IP publico."
      exit 0
    fi

    if echo "$SAIDA" | grep -qiE 'out of (host )?capacity'; then
      log "Sem capacidade em $AD."
    elif echo "$SAIDA" | grep -qiE 'TooManyRequests|429'; then
      log "A Oracle pediu para ir mais devagar; esperando o proximo ciclo."
      break
    else
      log "Erro diferente de falta de capacidade - parando para voce ver:"
      echo "$SAIDA"
      exit 1
    fi
    sleep 20
  done
  log "Nova rodada em ${INTERVALO} min."
  sleep $((INTERVALO * 60))
done

# ---------------------------------------------------------------------------------------------------------
# Configurar o OCI CLI (uma vez, no Mac):
#   1. brew install oci-cli
#   2. oci setup config      -> aceite os caminhos padrao; ele pede:
#        user OCID    = console > icone do perfil (canto superior direito) > My profile > OCID (Copy)
#        tenancy OCID = console > icone do perfil > Tenancy: <nome> > OCID (Copy)
#        region       = us-ashburn-1 (a sua home region)
#      e gera uma chave de API em ~/.oci/
#   3. Envie a chave PUBLICA da API: console > My profile > API keys > Add API key > Paste public key
#        conteudo de: cat ~/.oci/oci_api_key_public.pem
#   4. Teste: oci iam region list   (deve listar regioes, sem erro)
# ---------------------------------------------------------------------------------------------------------
