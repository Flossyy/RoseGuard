using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace RoseGuard.ViewModels;

public class LoginViewModel : INotifyPropertyChanged
{
    private string _userName;
    private string _password;
    private bool _isBusy;
    private string _errorMessage;

    public event PropertyChangedEventHandler PropertyChanged;

    public string UserName
    {
        get => _userName;
        set
        {
            if(_userName == value) return;
            _userName = value;
            OnPropertyChanged();
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if(_password == value) return;
            _password = value;
            OnPropertyChanged();
        }
    }

        public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if(_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotBusy));
        }
    }
    public bool IsNotBusy => !IsBusy;

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if(_errorMessage == value) return;
            _errorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public ICommand LoginCommand { get; }

    public ICommand UseLocalStorageCommand { get; }

    public LoginViewModel()
    {
        UseLocalStorageCommand = new Command(OnUseLocalStorage);
        LoginCommand = new Command(async () => await OnLoginAsync(), CanLogin);
    }

    private void OnUseLocalStorage(object obj)
    {
        //TODO Check local storage availability, for now we just simulate success
        ErrorMessage = string.Empty;
    }

    private bool CanLogin()
    {
        return IsNotBusy
                && !string.IsNullOrWhiteSpace(UserName)
                && !string.IsNullOrWhiteSpace(Password);
    }

    private async Task OnLoginAsync()
    {
        if (!CanLogin())
        {
            ErrorMessage = "Please enter both username and password.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            
            //TODO API
            await Task.Delay(1000);

            if (UserName == "test" && Password == "1234")
            {
                // In “clean” MVVM you would raise an event or use messaging/nav service.
                // For now we just set a status message.
                ErrorMessage = string.Empty;
                // TODO: expose a navigation event or a property for success
            }
            else
            {
                ErrorMessage = "Invalid credentials.";
            }
        }
        finally
        {
            IsBusy = false;
            (LoginCommand as Command)?.ChangeCanExecute();
        }
    }


    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        if(propertyName == nameof(UserName) || propertyName == nameof(Password) || propertyName == nameof(IsBusy))
        {
            (LoginCommand as Command)?.ChangeCanExecute();
        }
    }
}
