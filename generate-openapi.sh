#!/usr/bin/env bash
# Regenerate the OpenAPI spec (all endpoints, from code) for Postman import.
# Run this whenever you add/change/remove an endpoint, then re-import EDvanz.openapi.json
# into the Postman desktop app (Import -> pick the file -> "Update" the existing collection).
#
# Requires the Swashbuckle CLI once:  dotnet tool install -g Swashbuckle.AspNetCore.Cli
set -euo pipefail
cd "$(dirname "$0")"

SPEC_OBJ="Edvanz.API/obj/Debug/net10.0/EndpointInfo/Edvanz.API.json"   # path .postman/resources.yaml points at
SPEC_ROOT="EDvanz.openapi.json"                                        # convenient copy to import

echo "==> Building Edvanz.API ..."
dotnet build Edvanz.API/Edvanz.API.csproj -c Debug -v q

echo "==> Generating OpenAPI spec ..."
mkdir -p "$(dirname "$SPEC_OBJ")"
# Development env skips Azure Key Vault; the dummy connection string satisfies the
# boot guard. DB-touching startup (seeding/Hangfire) is try/catch-guarded, so no real
# database is needed just to emit the spec.
ASPNETCORE_ENVIRONMENT=Development \
ConnectionStrings__con="Server=127.0.0.1,11433;Database=EdvanzGen;User Id=sa;Password=NoRealDb123!;TrustServerCertificate=True;Connection Timeout=1" \
  swagger tofile --output "$SPEC_OBJ" Edvanz.API/bin/Debug/net10.0/Edvanz.API.dll v1

cp "$SPEC_OBJ" "$SPEC_ROOT"
echo "==> Done. Spec written to:"
echo "    $SPEC_OBJ   (Postman VS Code extension / .postman resource)"
echo "    $SPEC_ROOT  (import this into the Postman desktop app)"

# Also emit a ready-made Postman COLLECTION with all example responses baked in.
# Postman's own OpenAPI importer ignores parameter examples and its example handling
# varies by version/settings — importing this collection file directly sidesteps all
# of that (a collection import preserves saved examples verbatim, no settings asked).
COLLECTION="EDvanz.generated.postman_collection.json"
if command -v npx >/dev/null 2>&1; then
  echo "==> Generating Postman collection (openapi2postmanv2) ..."
  npx -y -p openapi-to-postmanv2 openapi2postmanv2 -s "$SPEC_ROOT" -o "$COLLECTION" -p \
      -O parametersResolution=Example,folderStrategy=Tags,requestNameSource=URL \
    && echo "    $COLLECTION  (import this file — every request carries its example responses)"
else
  echo "==> npx not found — skipped Postman collection generation ($COLLECTION)."
fi
