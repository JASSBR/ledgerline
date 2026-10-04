variable "subscription_id" {
  type        = string
  description = "Azure subscription to deploy into."
}

variable "location" {
  type        = string
  default     = "italynorth"
  description = "Azure region (Azure for Students only allows a few)."
}

variable "name" {
  type        = string
  default     = "ledgerline"
  description = "Prefix of every resource name."
}

variable "image_registry" {
  type        = string
  default     = "ghcr.io/jassbr"
  description = "Public registry holding the images, built and pushed by .github/workflows/images.yml."
}

variable "image_tag" {
  type        = string
  description = "Tag of the ledgerline-* images to run."
}

variable "spa_origin" {
  type        = string
  default     = "https://ledgerline-bank.vercel.app"
  description = "Origin of the Angular app, allowed by CORS on the gateway (and declared in the Keycloak realm)."
}

variable "existing_postgres_server" {
  type = object({
    name           = string
    resource_group = string
  })
  default     = null
  description = <<-EOT
    Reuse a PostgreSQL Flexible Server instead of creating one (its admin password then comes from
    postgres_admin_password). Null creates a dedicated Burstable B1ms server.
  EOT
}

variable "postgres_admin_login" {
  type    = string
  default = "ledgerline"
}

variable "postgres_admin_password" {
  type        = string
  default     = null
  sensitive   = true
  description = "Only with existing_postgres_server; a new server gets a generated password."
}

variable "existing_environment" {
  type = object({
    name           = string
    resource_group = string
  })
  default     = null
  description = <<-EOT
    Run the apps in an existing Container Apps environment (it must be in WorkloadProfiles mode). Azure for Students
    allows one environment per region. Null creates a dedicated one.
  EOT
}
