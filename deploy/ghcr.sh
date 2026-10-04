# Sourced by the deploy scripts. wait_for_image <name> <tag>: waits until ghcr.io/jassbr/<name>:<tag> exists.
# The images are public, so an anonymous pull token is enough to ask the registry.
wait_for_image() {
  local name=$1 tag=$2 token status
  echo "→ waiting for ghcr.io/jassbr/$name:$tag (built by GitHub Actions)"
  for _ in $(seq 1 120); do
    token=$(curl -s "https://ghcr.io/token?scope=repository:jassbr/$name:pull" | sed -nE 's/.*"token":"([^"]+)".*/\1/p')
    status=$(curl -s -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $token" \
      -H 'Accept: application/vnd.oci.image.index.v1+json, application/vnd.docker.distribution.manifest.v2+json' \
      "https://ghcr.io/v2/jassbr/$name/manifests/$tag")
    [ "$status" = 200 ] && return 0
    sleep 10
  done
  echo "✗ ghcr.io/jassbr/$name:$tag not found: is the commit pushed, the Images workflow green, the package public?"
  return 1
}
