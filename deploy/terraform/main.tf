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
  count               = var.existing_environment == null ? 1 : 0
  name                = "${var.name}-logs"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  sku                 = "PerGB2018"
  retention_in_days   = 30
}

# Declared through the ARM API: since 2026 a new environment defaults to "Express" mode, which refuses sidecar
# containers, and the azurerm provider cannot set the mode. WorkloadProfiles on the consumption profile still bills
# per second and scales to zero.
resource "azapi_resource" "environment" {
  count     = var.existing_environment == null ? 1 : 0
  type      = "Microsoft.App/managedEnvironments@2025-10-02-preview"
  name      = "${var.name}-apps"
  parent_id = azurerm_resource_group.this.id
  location  = azurerm_resource_group.this.location

  body = {
    properties = {
      environmentMode  = "WorkloadProfiles"
      workloadProfiles = [{ name = "Consumption", workloadProfileType = "Consumption" }]
      appLogsConfiguration = {
        destination = "log-analytics"
        logAnalyticsConfiguration = {
          customerId = azurerm_log_analytics_workspace.this[0].workspace_id
          sharedKey  = azurerm_log_analytics_workspace.this[0].primary_shared_key
        }
      }
    }
  }
  response_export_values = ["properties.defaultDomain"]
  # environmentMode is newer than the provider's embedded schema.
  schema_validation_enabled = false
}

data "azurerm_container_app_environment" "existing" {
  count               = var.existing_environment == null ? 0 : 1
  name                = var.existing_environment.name
  resource_group_name = var.existing_environment.resource_group
}

locals {
  environment_id     = var.existing_environment == null ? azapi_resource.environment[0].id : data.azurerm_container_app_environment.existing[0].id
  environment_domain = var.existing_environment == null ? azapi_resource.environment[0].output.properties.defaultDomain : data.azurerm_container_app_environment.existing[0].default_domain
}

resource "random_password" "rabbitmq" {
  length  = 32
  special = false
}

locals {
  registry       = var.image_registry
  keycloak_fqdn  = "${var.name}-auth.${local.environment_domain}"
  authority      = "https://${local.keycloak_fqdn}/realms/ledgerline"
  service_ports  = { ledger = 8081, payments = 8082, fraud = 8083 }
  service_sizing = { ledger = { cpu = 0.5, memory = "1Gi" }, payments = { cpu = 0.5, memory = "1Gi" }, fraud = { cpu = 0.25, memory = "0.5Gi" } }
}
