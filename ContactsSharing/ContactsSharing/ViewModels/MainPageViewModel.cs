using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ContactsSharing.Models;
using NickBuhro.Translit;
using Plugin.ContactService;
using Plugin.ContactService.Shared;
using Plugin.Permissions;
using Plugin.Permissions.Abstractions;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace ContactsSharing.ViewModels
{
    public class MainPageViewModel : INotifyPropertyChanged
    {
        #region fields

        private static readonly string ContactsFilePath = Path.Combine(FileSystem.CacheDirectory, "contacts.vcf");
        private IList<Contact> _initialData;

        #endregion

        public MainPageViewModel()
        {
            AppVersion = AppInfo.VersionString;
        }

        #region properties
        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnTextChanged();
                OnPropertyChanged();
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                ShareCommand.ChangeCanExecute();
                SettingsCommand.ChangeCanExecute();
                OnPropertyChanged();
            }
        }


        private ObservableCollection<ContactViewModel> _contacts;
        public ObservableCollection<ContactViewModel> Contacts
        {
            get => _contacts;
            set
            {
                _contacts = value;
                OnPropertyChanged();
            }
        }

        private bool _transliteration;
        public bool Transliteration
        {
            get => _transliteration;
            set
            {
                _transliteration = value;
                OnPropertyChanged();
            }
        }
        private bool _encodeQuotedPrintable;
        public bool EncodeQuotedPrintable
        {
            get => _encodeQuotedPrintable;
            set
            {
                _encodeQuotedPrintable = value;
                OnPropertyChanged();
            }
        }
        private bool _customMime;
        public bool CustomMime
        {
            get => _customMime;
            set
            {
                _customMime = value;
                OnPropertyChanged();
            }
        }
        private string _mimeType = "text/vcard";
        public string MimeType
        {
            get => _mimeType;
            set
            {
                _mimeType = value;
                OnPropertyChanged();
            }
        }

        private bool _settingsShown;
        public bool SettingsShown
        {
            get => _settingsShown;
            set
            {
                _settingsShown = value;
                OnPropertyChanged();
            }
        }
        private string _appVersion;
        public string AppVersion
        {
            get => _appVersion;
            set
            {
                _appVersion = value;
                OnPropertyChanged();
            }
        }


        private bool _isAllSelected = true;
        public bool IsAllSelected
        {
            get => _isAllSelected;
            set
            {
                _isAllSelected = value;
                OnPropertyChanged();
                HandleIsAllSelected();
            }
        }

        private bool _isAllTransliterated;
        public bool IsAllTransliterated
        {
            get => _isAllTransliterated;
            set
            {
                _isAllTransliterated = value;
                OnPropertyChanged();

            }
        }

        private void HandleIsAllSelected()
        {
            if (Contacts == null) return;
            foreach (ContactViewModel contact in Contacts)
            {
                contact.IsSelected = IsAllSelected;
            }
        }
        #endregion

        #region commands

        private Command _globalTransliterateCommand;

        public Command GlobalTransliterateCommand => _globalTransliterateCommand ??= new Command(OnGlobalTransliterate, () => !IsBusy);
        private void OnGlobalTransliterate()
        {

            IsAllTransliterated = !IsAllTransliterated;
            if (Contacts == null) return;
            foreach (ContactViewModel contact in Contacts)
            {
                contact.Transliterated = IsAllTransliterated;
                contact.Name = contact.Transliterated ? NickBuhro.Translit.Transliteration.CyrillicToLatin(contact.Name, Language.Russian) : contact.OriginalName;
            }
        }




        private Command _shareCommand;
        public Command ShareCommand => _shareCommand ??= new Command(OnShare, () => !IsBusy);
        private async void OnShare()
        {
            try
            {
                IsBusy = true;

                if (Contacts == null)
                    return;

                var contacts = Contacts.Where(d => d.IsSelected).ToList();
                if (!contacts.Any())
                    return;
                var result = await Application.Current.MainPage.DisplayAlert("Contacts loaded", $"Total count: {contacts.Count}", "OK",
                    "Cancel");
                if (!result) return;

                var builder = new StringBuilder();
                foreach (var contact in contacts)
                {
                    await GetVCard(builder, contact.Contact);
                }

                if (File.Exists(ContactsFilePath))
                    File.Delete(ContactsFilePath);
                await using (var file = File.Create(ContactsFilePath))
                {
                    await using var writer = new StreamWriter(file);
                    await writer.WriteAsync(builder.ToString());
                }

                if (CustomMime && !string.IsNullOrEmpty(_mimeType))
                {
                    await Share.RequestAsync(new ShareFileRequest("Contacts",
                        new ShareFile(ContactsFilePath, _mimeType)));
                }
                else
                {
                    await Share.RequestAsync(new ShareFileRequest("Contacts",
                        new ShareFile(ContactsFilePath)));
                }

            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }
        private Command _settingsCommand;
        public Command SettingsCommand => _settingsCommand ??= new Command(OnSettings, () => !IsBusy);
        private void OnSettings()
        {
            SettingsShown = !SettingsShown;
        }

        #endregion

        #region private methods

        private ValueTask GetVCard(StringBuilder builder, Contact contact)
        {
            if (string.IsNullOrEmpty(contact.Name))
                return new ValueTask();
            if (string.IsNullOrEmpty(contact.Number) && (contact.Numbers == null || !contact.Numbers.Any()))
                return new ValueTask();
            builder.AppendLine("BEGIN:VCARD");
            builder.AppendLine("VERSION:2.1");
            string name = ContainCyrillic(contact.Name) && Transliteration ? NickBuhro.Translit.Transliteration.CyrillicToLatin(contact.Name, Language.Russian) : contact.Name;
            if (ContainCyrillic(contact.Name) && !Transliteration && EncodeQuotedPrintable)
            {
                // Name        
                builder.Append("N;CHARSET=UTF-8;ENCODING=QUOTED-PRINTABLE:;").Append(EncodeQuoted(name))
                    .AppendLine(";;;");
            }
            else
            {
                // Name        
                builder.Append("N:").Append(name)
                    .AppendLine(";;;");
            }

            if (ContainCyrillic(contact.Name) && !Transliteration && EncodeQuotedPrintable)
            {
                // Full name        
                builder.Append("FN;CHARSET=UTF-8;ENCODING=QUOTED-PRINTABLE:;").AppendLine(EncodeQuoted(name));
            }
            else
            {
                // Full name        
                builder.Append("FN:").AppendLine(name);
            }

            if (string.IsNullOrEmpty(contact.Number))
            {
                if (contact.Numbers != null && contact.Numbers.Any())
                {
                    builder.Append("TEL;CELL:").AppendLine(contact.Numbers.FirstOrDefault());
                }
            }
            else
            {
                builder.Append("TEL;CELL:").AppendLine(contact.Number);
            }

            builder.AppendLine("END:VCARD");
            return new ValueTask();
        }

        private bool ContainCyrillic(string str)
        {
            return Regex.IsMatch(str, @"\p{IsCyrillic}");
        }

        private string EncodeQuoted(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            StringBuilder builder = new StringBuilder();

            byte[] bytes = Encoding.UTF8.GetBytes(value);
            foreach (byte v in bytes)
            {
                // The following are not required to be encoded:
                // - Tab (ASCII 9)
                // - Space (ASCII 32)
                // - Characters 33 to 126, except for the equal sign (61).

                if ((v == 9) || ((v >= 32) && (v <= 60)) || ((v >= 62) && (v <= 126)))
                {
                    builder.Append(Convert.ToChar(v));
                }
                else
                {
                    builder.Append('=');
                    builder.Append(v.ToString("X2"));
                }
            }

            char lastChar = builder[^1];
            if (char.IsWhiteSpace(lastChar))
            {
                builder.Remove(builder.Length - 1, 1);
                builder.Append('=');
                builder.Append(((int)lastChar).ToString("X2"));
            }

            return builder.ToString();
        }


        private async Task<IList<Contact>> GetContacts()
        {
            var status = await CrossPermissions.Current.CheckPermissionStatusAsync<ContactsPermission>();
            if (status != Plugin.Permissions.Abstractions.PermissionStatus.Granted)
            {
                if (await CrossPermissions.Current.ShouldShowRequestPermissionRationaleAsync(Permission.Contacts))
                {
                    await Application.Current.MainPage.DisplayAlert("Need Contacts", "Gunna need that Contacts", "OK");
                }

                var results = await CrossPermissions.Current.RequestPermissionAsync<ContactsPermission>();
                status = results;
            }

            if (status == Plugin.Permissions.Abstractions.PermissionStatus.Granted)
            {
                return await DependencyService.Get<IContactService>().GetContactListAsync();
            }

            if (status != Plugin.Permissions.Abstractions.PermissionStatus.Unknown)
            {
                await Application.Current.MainPage.DisplayAlert("Contacts Denied", "Can not continue, try again.", "OK");
            }

            return null;
        }

        private bool _isTextChanging;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private async void OnTextChanged()
        {
            try
            {
                if (_isTextChanging)
                {
                    _cts.Cancel();
                    _cts.Dispose();
                    _cts = new CancellationTokenSource();
                }
                _isTextChanging = true;
                await Task.Delay(700, _cts.Token);
                if (_initialData == null)
                    return;
                Contacts = !string.IsNullOrEmpty(SearchText)
                    ? new ObservableCollection<ContactViewModel>(_initialData.Where(d => d.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).OrderBy(d => d.Name).Select(d => new ContactViewModel(d) { IsSelected = true }))
                    : new ObservableCollection<ContactViewModel>(_initialData.OrderBy(d => d.Name).Select(d => new ContactViewModel(d) { IsSelected = true }));
            }
            catch (OperationCanceledException) { }
            finally
            {
                _isTextChanging = false;
            }
        }

        #endregion

        public async void Initialize()
        {
            try
            {
                IsBusy = true;
                _initialData = await GetContacts();
                if (_initialData == null || !_initialData.Any())
                    return;

                Contacts = new ObservableCollection<ContactViewModel>(_initialData.OrderBy(d => d.Name).Select(d => new ContactViewModel(d) { IsSelected = true }));
            }
            finally
            {
                IsBusy = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}
