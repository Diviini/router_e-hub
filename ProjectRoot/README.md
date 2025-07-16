# EmitterHub - Routeur eHub vers Art-Net

EmitterHub est une application console .NET conçue pour servir de pont entre une source de données utilisant un protocole personnalisé "eHub" et un système de contrôle d'éclairage standardisé via Art-Net. L'application écoute les messages d'état d'entités, les mappe à des univers et canaux DMX spécifiques, et les transmet en temps réel pour piloter des installations LED ou d'autres équipements DMX.

## Fonctionnalités

- **Réception de données eHub** : Écoute sur un port UDP (par défaut 8765) les messages entrants.
- **Mappage DMX** : Utilise un fichier CSV pour mapper les identifiants d'entités eHub à des adresses Art-Net (Univers et Canal).
- **Transmission Art-Net** : Envoie les données DMX formatées sous forme de paquets Art-Net.
- **Traitement Synchrone** : Opère dans une boucle temps réel à haute fréquence (environ 40Hz) pour assurer une faible latence.
- **Statistiques en direct** : Affiche des statistiques de fonctionnement (messages reçus, paquets envoyés, etc.) sans interrompre le service.

## Architecture

Le projet est structuré autour de plusieurs composants clés :

- **`EHubReceiver`** : Responsable de l'écoute et de la réception des paquets de données sur le réseau.
- **`CsvMappingLoader` & `DmxMapper`** : Chargent la configuration de mappage à partir du fichier `mapping_clean.csv` et l'appliquent aux données reçues.
- **`Router`** : Le cœur de l'application. Il orchestre le flux de données : il récupère les messages du `EHubReceiver`, utilise le `DmxMapper` pour déterminer la destination, et prépare les trames DMX.
- **`ArtNetSender`** : Prend les trames DMX préparées par le routeur, les encapsule dans des paquets Art-Net et les envoie sur le réseau.
- **`Main`** : Le point d'entrée de l'application qui initialise tous les composants et gère la boucle principale de traitement.

## Configuration

La configuration principale du routage se trouve dans le fichier :
`EmitterHub/Config/mapping_clean.csv`

Ce fichier CSV définit comment chaque entité du système eHub est traduite en une adresse DMX. Il doit contenir les colonnes suivantes :

- `entity_id` : L'identifiant unique de l'entité source.
- `universe` : L'univers Art-Net de destination.
- `channel` : Le canal DMX de départ sur l'univers spécifié.

## Lancement

1.  Compilez le projet à l'aide d'un environnement de développement .NET.
2.  Exécutez l'application.

Une fois lancée, l'application commence immédiatement à écouter les paquets eHub et à les router.

### Commandes interactives

- Appuyez sur la touche **`s`** pour afficher les statistiques de routage actuelles.
- Appuyez sur la touche **`q`** pour arrêter l'application proprement.
