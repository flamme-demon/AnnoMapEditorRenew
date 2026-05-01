using System;
using System.IO;
using System.Threading.Tasks;
using AnnoMapEditor.DataArchives;
using AnnoMapEditor.Games;
using AnnoMapEditor.Utilities;

namespace AnnoMapEditor.UI.Avalonia.ViewModels
{
    public class StartWindowViewModel : ObservableBase
    {
        public string GamePath
        {
            get => Settings.Instance.GamePath ?? string.Empty;
            set
            {
                Settings.Instance.GamePath = string.IsNullOrWhiteSpace(value) ? null : value;
                OnPropertyChanged(nameof(GamePath));
                OnPropertyChanged(nameof(GamePathExists));
                OnPropertyChanged(nameof(CanContinue));
            }
        }

        public bool GamePathExists => !string.IsNullOrWhiteSpace(GamePath) && Directory.Exists(GamePath);

        public bool CanContinue => GamePathExists && !IsInitializing;

        public bool IsInitializing
        {
            get => _isInitializing;
            private set
            {
                if (SetProperty(ref _isInitializing, value))
                    OnPropertyChanged(nameof(CanContinue));
            }
        }
        private bool _isInitializing;

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }
        private string _statusMessage = string.Empty;

        public Game? DetectedGame
        {
            get => _detectedGame;
            private set => SetProperty(ref _detectedGame, value);
        }
        private Game? _detectedGame;

        public bool InitializationSucceeded
        {
            get => _initializationSucceeded;
            private set => SetProperty(ref _initializationSucceeded, value);
        }
        private bool _initializationSucceeded;

        public async Task<bool> InitializeAsync()
        {
            if (string.IsNullOrWhiteSpace(GamePath))
            {
                StatusMessage = "Sélectionne d'abord le dossier du jeu.";
                return false;
            }

            IsInitializing = true;
            StatusMessage = "Détection du jeu et chargement des assets…";
            InitializationSucceeded = false;

            try
            {
                string? dataPath = Settings.Instance.DataPath ?? Path.Combine(GamePath, "maindata");
                await DataManager.Instance.TryInitializeAsync(dataPath);

                DetectedGame = DataManager.Instance.DetectedGame;

                if (DataManager.Instance.HasError)
                {
                    StatusMessage = $"Échec : {DataManager.Instance.ErrorMessage}";
                    InitializationSucceeded = false;
                    return false;
                }

                if (DataManager.Instance.IsInitialized)
                {
                    StatusMessage = $"Jeu détecté : {DetectedGame?.Title ?? "?"}";
                    InitializationSucceeded = true;
                    return true;
                }

                StatusMessage = "État inconnu après initialisation.";
                return false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur : {ex.Message}";
                InitializationSucceeded = false;
                return false;
            }
            finally
            {
                IsInitializing = false;
            }
        }
    }
}
