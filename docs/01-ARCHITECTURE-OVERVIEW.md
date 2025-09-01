# DriveOps - Architecture Overview

## 🎯 Vision du Projet

DriveOps est une plateforme SAAS modulaire dédiée à l'industrie automobile, proposant une solution complète pour :
- Garages et ateliers de réparation
- Services de dépannage et assistance
- Locations de véhicules
- Gestion de flottes automobiles

## 🏗️ Principe Architectural : Une Instance Par Client

### Avantages
- **Isolation totale** des données client
- **Sécurité maximale** (pas de risque de fuite entre clients)
- **Personnalisation poussée** par instance
- **Scaling horizontal** naturel
- **Conformité réglementaire** facilitée (RGPD, secteur automobile)
- **Facturation flexible** par modules activés

### Architecture Technique

```
DriveOps Platform Template
├── Core Framework          # Fondations partagées
├── Modules Library         # Bibliothèque de modules
├── Deployment Engine       # Automatisation déploiement
└── Management Portal       # Gestion des instances

Client Instance (Déployée)
├── Core Runtime            # Runtime spécifique
├── Enabled Modules         # Modules activés
├── Client Configuration    # Config personnalisée
├── Data Storage           # PostgreSQL + MongoDB isolés
└── Keycloak Instance      # Auth isolée
```

## 🛠️ Stack Technique

### Frontend
- **Blazor WASM** : Applications client riches
- **Blazor Server** : Pages publiques et administration
- **Radzen** : Composants UI responsifs
- **Progressive Web App** : Support mobile/offline

### Backend
- **.NET 8+** : API REST et gRPC
- **MediatR** : Pattern CQRS pour les modules
- **FluentValidation** : Validation des données
- **AutoMapper** : Mapping entités/DTOs

### Données
- **PostgreSQL** : Données relationnelles (clients, factures, véhicules)
- **MongoDB** : Documents (historiques, logs, fichiers)
- **Redis** : Cache et sessions
- **MinIO** : Stockage fichiers (documents signés)

### Authentification & Sécurité
- **Keycloak** : SSO et gestion des permissions
- **JWT** : Tokens d'authentification
- **HTTPS** : Chiffrement obligatoire
- **Audit Trail** : Traçabilité complète

### Infrastructure
- **Docker** : Containerisation
- **Kubernetes** : Orchestration (optionnel)
- **PostgreSQL HA** : Haute disponibilité
- **Backup automatisé** : Sauvegarde quotidienne

## 🔧 Architecture Modulaire

### Modules Core (Toujours présents)
- **Users & Permissions** : Gestion utilisateurs
- **Vehicles Base** : Référentiel véhicules partagé
- **Billing & Subscriptions** : Facturation modulaire
- **Notifications** : Email, SMS, notifications

### Modules Métier (Facturation à la carte)
- **Garage** : Interventions, pièces, main d'œuvre
- **Breakdown** : Dépannage, géolocalisation, missions
- **Rental** : Location, réservations, contrats
- **Fleet** : Gestion de flotte, maintenance préventive
- **Accounting** : Comptabilité avancée
- **CRM** : Gestion relation client

### Modules Transverses (Optionnels)
- **Document Generation** : Factures, devis, contrats
- **Digital Signature** : OpenSign integration
- **Analytics** : Tableaux de bord et KPIs
- **API Gateway** : Intégrations tierces

## 🚀 Stratégie de Déploiement

### Template d'Instance
1. **Configuration initiale** : Sélection modules, paramètres
2. **Déploiement automatisé** : Infrastructure + application
3. **Configuration Keycloak** : Utilisateurs et permissions
4. **Initialisation données** : Schémas et données de base
5. **Tests de validation** : Vérification fonctionnement
6. **Mise en production** : DNS + certificats SSL

### Gestion du Cycle de Vie
- **Updates modulaires** : Mise à jour par module
- **Rollback automatique** : En cas d'échec
- **Monitoring continu** : Santé des instances
- **Backup/Restore** : Sauvegarde et restauration

## 📈 Modèle Commercial

### Facturation Modulaire
- **Base mensuelle** : Core modules inclus
- **Modules à la carte** : Facturation additionnelle
- **Utilisateurs supplémentaires** : Prix par utilisateur
- **Storage additionnel** : Au-delà des quotas inclus

### Tiers de Service
- **Starter** : Modules de base, 1-5 utilisateurs
- **Professional** : Modules avancés, 6-20 utilisateurs  
- **Enterprise** : Tous modules, utilisateurs illimités

## 🎯 Prochaines Étapes

1. **Définition détaillée des modules** (En cours)
2. **Modèle de données** (PostgreSQL + MongoDB)
3. **Architecture des permissions** (Keycloak + modules)
4. **POC technique** (Structure .NET + premier module)
5. **Infrastructure de déploiement** (Docker + automation)

---
*Document créé le : 2025-09-01*
*Dernière mise à jour : 2025-09-01*