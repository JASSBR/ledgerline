output "gateway_url" {
  value = "https://${azurerm_container_app.bank.ingress[0].fqdn}"
}

output "keycloak_url" {
  value = "https://${local.keycloak_fqdn}"
}

output "authority" {
  value = local.authority
}
