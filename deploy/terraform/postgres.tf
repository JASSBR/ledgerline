# One database per service on one server: isolation of data ownership without paying for three servers.

resource "random_password" "postgres" {
  length  = 32
  special = false
}

resource "azurerm_postgresql_flexible_server" "this" {
  count                         = var.existing_postgres_server == null ? 1 : 0
  name                          = "${var.name}-pg-${random_string.suffix.result}"
  resource_group_name           = azurerm_resource_group.this.name
  location                      = azurerm_resource_group.this.location
  version                       = "17"
  sku_name                      = "B_Standard_B1ms"
  storage_mb                    = 32768
  administrator_login           = var.postgres_admin_login
  administrator_password        = random_password.postgres.result
  public_network_access_enabled = true
  zone                          = "1"
  backup_retention_days         = 7
}

resource "azurerm_postgresql_flexible_server_firewall_rule" "azure_services" {
  count = var.existing_postgres_server == null ? 1 : 0
  # 0.0.0.0 is Azure's convention for "Azure services only", not the open internet.
  name             = "allow-azure-services"
  server_id        = azurerm_postgresql_flexible_server.this[0].id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

data "azurerm_postgresql_flexible_server" "existing" {
  count               = var.existing_postgres_server == null ? 0 : 1
  name                = var.existing_postgres_server.name
  resource_group_name = var.existing_postgres_server.resource_group
}

locals {
  postgres_server_id = var.existing_postgres_server == null ? azurerm_postgresql_flexible_server.this[0].id : data.azurerm_postgresql_flexible_server.existing[0].id
  postgres_host      = var.existing_postgres_server == null ? azurerm_postgresql_flexible_server.this[0].fqdn : data.azurerm_postgresql_flexible_server.existing[0].fqdn
  postgres_login     = var.existing_postgres_server == null ? var.postgres_admin_login : data.azurerm_postgresql_flexible_server.existing[0].administrator_login
  postgres_password  = var.existing_postgres_server == null ? random_password.postgres.result : var.postgres_admin_password
  services           = toset(["ledger", "payments", "fraud"])
}

resource "azurerm_postgresql_flexible_server_database" "service" {
  for_each  = local.services
  name      = "${var.name}_${each.key}"
  server_id = local.postgres_server_id
  charset   = "UTF8"
  collation = "en_US.utf8"
}
