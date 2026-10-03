terraform {
  required_version = ">= 1.9"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.40"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.7"
    }
  }
  # Local state by default (gitignored: it holds generated passwords). For a team, use the azurerm backend.
}

provider "azurerm" {
  features {}
  subscription_id = var.subscription_id
}
