# E-Hub to Art-Net Router

Ce projet est une application .NET conçue pour servir de pont entre un système nommé "E-Hub" et des équipements d'éclairage contrôlables via le protocole Art-Net (DMX). L'application reçoit des états d'entités, les mappe sur des canaux DMX selon une configuration définie, et envoie les données en temps réel à des contrôleurs d'éclairage.

Une interface utilisateur, développée avec Avalonia, permet de visualiser les statistiques et l'état du routeur.

## ✨ Fonctionnalités

- **Réception de données** : Écoute et reçoit les états d'entités depuis E-Hub.
- **Mappage DMX** : Traduit les données reçues en valeurs pour des canaux DMX spécifiques en utilisant un fichier de mapping CSV.
- **Émission Art-Net** : Crée et envoie des paquets Art-Net sur le réseau pour contrôler des appareils DMX.
- **Interface de monitoring** : Une application de bureau pour suivre en temps réel les données transmises et l'état du système.

## 🛠️ Technologies utilisées

- **Langage** : C#
- **Framework** : .NET 8
- **Interface Utilisateur** : Avalonia UI
- **Protocoles** : Art-Net, DMX

## 📂 Structure du projet

- **`router_e-hub.sln`**: Le fichier de solution principal pour Visual Studio.
- **`ProjectRoot/`**: Contient la logique métier principale du routeur.
  - **`EmitterHub/`**: Le cœur de l'application.
    - `eHub/`: Logique de réception des données depuis E-Hub.
    - `DMX/`: Gestion du mapping et de la création des trames DMX.
    - `ArtNet/`: Logique d'envoi des paquets Art-Net.
    - `Routing/`: Orchestration du flux de données entre la réception et l'envoi.
  - **`Main.cs`**: Point d'entrée de l'application console/service.
- **`EmitterHub.UI/`**: L'application de bureau Avalonia pour le monitoring.
  - `Views/`: Les fenêtres et contrôles de l'interface.
  - `ViewModels/`: Les modèles de vue qui exposent les données à l'interface.
  - `Program.cs`: Point d'entrée de l'application UI.

## 🚀 Pour commencer

### Prérequis

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- (Optionnel) [Visual Studio 2022](https://visualstudio.microsoft.com/)

### Compilation

Ouvrez un terminal à la racine du projet et exécutez la commande suivante pour compiler la solution complète :

```bash
dotnet build router_e-hub.sln
```

### Lancement

Le projet contient deux exécutables : le routeur (application console) et l'interface de monitoring (application UI).

**Pour lancer le routeur seul :**
```bash
dotnet run --project ProjectRoot/EmitterHub.csproj
```

**Pour lancer le routeur et l'interface de monitoring :**
```bash
dotnet run --project EmitterHub.UI/EmitterHub.UI.csproj
```

## ⚙️ Configuration

Le mapping entre les données reçues d'E-Hub et les canaux DMX est géré par le fichier `mapping_clean.csv` situé dans `ProjectRoot/EmitterHub/Config/`.

Ce fichier permet de définir quelle entité ou quel événement doit affecter un canal DMX spécifique.
