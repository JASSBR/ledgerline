# Keycloak: the identity provider, on its own public URL (browsers sign in there).
# Dev mode with the realm baked into the image (deploy/keycloak/Dockerfile): stateless, re-imported at every start.
resource "azurerm_container_app" "keycloak" {
  name                         = "${var.name}-auth"
  resource_group_name          = azurerm_resource_group.this.name
  container_app_environment_id = local.environment_id
  revision_mode                = "Single"
  workload_profile_name        = "Consumption"

  secret {
    name  = "keycloak-admin-password"
    value = random_password.keycloak_admin.result
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    min_replicas = 0
    max_replicas = 1

    # Keycloak takes ~30 s to start, the first thing a visitor waits for: kept up on weekday working hours, when
    # recruiters look, and woken by the first request otherwise. A custom rule replaces the default HTTP one, hence both.
    custom_scale_rule {
      name             = "weekday-hours"
      custom_rule_type = "cron"
      metadata = {
        timezone        = "Europe/Paris"
        start           = "0 8 * * 1-5"
        end             = "0 20 * * 1-5"
        desiredReplicas = "1"
      }
    }

    http_scale_rule {
      name                = "http"
      concurrent_requests = "50"
    }

    container {
      name   = "keycloak"
      image  = "${local.registry}/ledgerline-keycloak:${var.image_tag}"
      cpu    = 0.5
      memory = "1Gi"
      args   = ["start-dev", "--import-realm"]

      env {
        name  = "KC_HOSTNAME"
        value = "https://${local.keycloak_fqdn}"
      }
      env {
        # TLS ends at the Container Apps ingress; Keycloak trusts its X-Forwarded-* headers.
        name  = "KC_PROXY_HEADERS"
        value = "xforwarded"
      }
      env {
        name  = "KC_BOOTSTRAP_ADMIN_USERNAME"
        value = "admin"
      }
      env {
        name        = "KC_BOOTSTRAP_ADMIN_PASSWORD"
        secret_name = "keycloak-admin-password"
      }
    }
  }

}

resource "random_password" "keycloak_admin" {
  length  = 32
  special = false
}

# The bank: gateway, three services and RabbitMQ as containers of one app, talking over localhost.
# Why one app (ADR 0009): six always-on apps would cost about €150 a month; this one scales to zero when nobody
# uses the demo. It must stay at one replica, since each replica would carry its own broker.
# deploy/k8s describes the same system as separate deployments, as it would run in production.
resource "azurerm_container_app" "bank" {
  name                         = "${var.name}-bank"
  resource_group_name          = azurerm_resource_group.this.name
  container_app_environment_id = local.environment_id
  revision_mode                = "Single"
  workload_profile_name        = "Consumption"

  # Burstable B1ms accepts 50 connections in total, possibly shared: 3 services × a pool of 6 leaves room.
  dynamic "secret" {
    for_each = local.services
    content {
      name  = "${secret.key}-db"
      value = "Host=${local.postgres_host};Database=${var.name}_${secret.key};Username=${local.postgres_login};Password=${local.postgres_password};Ssl Mode=Require;Maximum Pool Size=6"
    }
  }

  secret {
    name  = "rabbitmq-password"
    value = random_password.rabbitmq.result
  }

  secret {
    name  = "messaging"
    value = "amqp://ledgerline:${random_password.rabbitmq.result}@localhost:5672"
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    transport        = "auto"
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    min_replicas = 0
    max_replicas = 1

    http_scale_rule {
      name                = "http"
      concurrent_requests = "50"
    }

    container {
      name   = "rabbitmq"
      image  = "docker.io/library/rabbitmq:4"
      cpu    = 0.5
      memory = "1Gi"
      env {
        name  = "RABBITMQ_DEFAULT_USER"
        value = "ledgerline"
      }
      env {
        name        = "RABBITMQ_DEFAULT_PASS"
        secret_name = "rabbitmq-password"
      }
    }

    dynamic "container" {
      for_each = local.service_ports
      content {
        name   = container.key
        image  = "${local.registry}/ledgerline-${container.key}:${var.image_tag}"
        cpu    = local.service_sizing[container.key].cpu
        memory = local.service_sizing[container.key].memory

        env {
          name  = "ASPNETCORE_HTTP_PORTS"
          value = tostring(container.value)
        }
        env {
          name        = "ConnectionStrings__${container.key}db"
          secret_name = "${container.key}-db"
        }
        env {
          name        = "ConnectionStrings__messaging"
          secret_name = "messaging"
        }
        env {
          name  = "Auth__Authority"
          value = local.authority
        }
        env {
          # Only the Ledger seeds the demo bank; the flag is harmless elsewhere.
          name  = "Demo__Seed"
          value = container.key == "ledger" ? "true" : "false"
        }
      }
    }

    container {
      name   = "gateway"
      image  = "${local.registry}/ledgerline-gateway:${var.image_tag}"
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "ASPNETCORE_HTTP_PORTS"
        value = "8080"
      }
      dynamic "env" {
        for_each = local.service_ports
        content {
          name  = "services__${env.key}__http__0"
          value = "http://localhost:${env.value}"
        }
      }
      dynamic "env" {
        for_each = var.spa_origins
        content {
          name  = "Cors__AllowedOrigins__${env.key}"
          value = env.value
        }
      }

      startup_probe {
        transport               = "HTTP"
        port                    = 8080
        path                    = "/alive"
        interval_seconds        = 3
        failure_count_threshold = 30
      }
      readiness_probe {
        transport = "HTTP"
        port      = 8080
        path      = "/health"
      }
    }
  }

  depends_on = [azurerm_postgresql_flexible_server_database.service]
}
