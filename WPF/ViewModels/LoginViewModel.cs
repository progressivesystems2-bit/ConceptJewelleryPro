using ConceptJewelleryPro.Application.Authentication;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Settings;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class LoginViewModel : ViewModelBase
{
    private readonly IAuthenticationService _authenticationService;

    private string _username = string.Empty;
    private string _password = string.Empty;
    private string? _errorMessage;
    private bool _isBusy;

    public LoginViewModel(IAuthenticationService authenticationService, DatabaseSettingsStore settingsStore)
    {
        _authenticationService = authenticationService;
        ConnectionSettings = new ConnectionSettingsViewModel(settingsStore);
        LoginCommand = new AsyncRelayCommand(LoginAsync, () => !IsBusy);
    }

    public event EventHandler<User>? LoginSucceeded;

    public string Username
    {
        get => _username;
        set
        {
            if (SetProperty(ref _username, value))
            {
                ClearError();
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                ClearError();
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                LoginCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public AsyncRelayCommand LoginCommand { get; }

    public ConnectionSettingsViewModel ConnectionSettings { get; }

    private async Task LoginAsync()
    {
        IsBusy = true;
        ClearError();

        try
        {
            var result = await _authenticationService.AuthenticateAsync(Username, Password);

            if (result.Succeeded)
            {
                Password = string.Empty;
                LoginSucceeded?.Invoke(this, result.User!);
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"An unexpected error occurred: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearError() => ErrorMessage = null;
}
