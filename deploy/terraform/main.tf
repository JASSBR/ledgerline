resource "random_string" "suffix" {
  length  = 6
  special = false
  upper   = false
}

resource "azurerm_resource_group" "this" {
  name     = "rg-${var.name}"
  location = var.location
}

resource "azurerm_log_analytics_workspace" "this" {
  name                = "${var.name}-logs"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  sku                 = "PerGB2018"
  retention_in_days   = 30
}

resource "azurerm_container_app_environment" "this" {
  name                       = "${var.name}-env"
  resource_group_name        = azurerm_resource_group.this.name
  location                   = azurerm_resource_group.this.location
  log_analytics_workspace_id = azurerm_log_analytics_workspace.this.id

  # Workload-profiles environment on the consumption profile: pay per second of use, scale to zero.
  workload_profile {
    name                  = "Consumption"
    workload_profile_type = "Consumption"
  }
}

data "azurerm_container_registry" "this" {
  name                = var.registry_name
  resource_group_name = var.registry_resource_group
}

# The apps pull images with their own identity: no registry password anywhere.
resource "azurerm_user_assigned_identity" "pull" {
  name                = "${var.name}-pull"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
}

resource "azurerm_role_assignment" "pull" {
  scope                = data.azurerm_container_registry.this.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.pull.principal_id
}

resource "random_password" "rabbitmq" {
  length  = 32
  special = false
}

locals {
  registry       = data.azurerm_container_registry.this.login_server
  keycloak_fqdn  = "${var.name}-auth.${azurerm_container_app_environment.this.default_domain}"
  authority      = "https://${local.keycloak_fqdn}/realms/ledgerline"
  service_ports  = { ledger = 8081, payments = 8082, fraud = 8083 }
  service_sizing = { ledger = { cpu = 0.5, memory = "1Gi" }, payments = { cpu = 0.5, memory = "1Gi" }, fraud = { cpu = 0.25, memory = "0.5Gi" } }
}
