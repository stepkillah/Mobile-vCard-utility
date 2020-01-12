using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NickBuhro.Translit;
using Plugin.ContactService;
using Plugin.ContactService.Shared;
using Plugin.Permissions;
using Plugin.Permissions.Abstractions;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace ContactsSharing
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(false)]
    public partial class MainPage : ContentPage
    {

        private bool _isTransliterate = false;
        private bool _isEncodeToQuoted = false;
        private bool _isCustomMime = false;
        private string _mimeType;
        public MainPage()
        {
            InitializeComponent();
            VersionSpan.Text = AppInfo.VersionString;
        }

        private async void LoadContacts()
        {
            try
            {
                IsBusy = true;
                var status = await CrossPermissions.Current.CheckPermissionStatusAsync(Permission.Contacts);
                if (status != PermissionStatus.Granted)
                {
                    if (await CrossPermissions.Current.ShouldShowRequestPermissionRationaleAsync(Permission.Contacts))
                    {
                        await DisplayAlert("Need Contacts", "Gunna need that Contacts", "OK");
                    }

                    var results = await CrossPermissions.Current.RequestPermissionsAsync(Permission.Contacts);
                    status = results[Permission.Contacts];
                }

                if (status == PermissionStatus.Granted)
                {
                    var contacts = await DependencyService.Get<IContactService>().GetContactListAsync();
                    var result = await DisplayAlert("Contacts loaded", $"Total count: {contacts.Count}", "OK",
                        "Cancel");
                    if (result)
                    {
                        var builder = new StringBuilder();
                        foreach (Contact contact in contacts)
                        {
                            await GetVCard(builder, contact);
                        }

                        if (File.Exists(ContactsFilePath))
                            File.Delete(ContactsFilePath);
                        using (var file = File.Create(ContactsFilePath))
                        {
                            using (var writer = new StreamWriter(file))
                            {
                                await writer.WriteAsync(builder.ToString());
                            }
                        }

                        if (_isCustomMime && !string.IsNullOrEmpty(_mimeType))
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
                }
                else if (status != PermissionStatus.Unknown)
                {
                    await DisplayAlert("Contacts Denied", "Can not continue, try again.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OnLoadContacts(object sender, EventArgs e)
        {
            LoadContacts();
        }

        public static string ContactsFilePath = Path.Combine(FileSystem.CacheDirectory, "contacts.vcf");
        private ValueTask GetVCard(StringBuilder builder, Contact contact)
        {
            if (string.IsNullOrEmpty(contact.Name))
                return new ValueTask();
            if (string.IsNullOrEmpty(contact.Number) && (contact.Numbers == null || !contact.Numbers.Any()))
                return new ValueTask();
            builder.AppendLine("BEGIN:VCARD");
            builder.AppendLine("VERSION:2.1");
            string name = ContainCyrillic(contact.Name) && _isTransliterate ? Transliteration.CyrillicToLatin(contact.Name, Language.Russian) : contact.Name;
            if (ContainCyrillic(contact.Name) && !_isTransliterate && _isEncodeToQuoted)
            {
                // Name        
                builder.Append("N;CHARSET=UTF-8;ENCODING=QUOTED-PRINTABLE:;").Append(EncodeQuotedPrintable(name))
                    .AppendLine(";;;");
            }
            else
            {
                // Name        
                builder.Append("N:").Append(name)
                    .AppendLine(";;;");
            }

            if (ContainCyrillic(contact.Name) && !_isTransliterate && _isEncodeToQuoted)
            {
                // Full name        
                builder.Append("FN;CHARSET=UTF-8;ENCODING=QUOTED-PRINTABLE:;").AppendLine(EncodeQuotedPrintable(name));
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



        private void TransliterateChanged(object sender, CheckedChangedEventArgs e)
        {
            _isTransliterate = e.Value;
        }

        private bool ContainCyrillic(string str)
        {
            return Regex.IsMatch(str, @"\p{IsCyrillic}");
        }

        private string EncodeQuotedPrintable(string value)
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

        private void OnEncodeQuotedChanged(object sender, CheckedChangedEventArgs e)
        {
            _isEncodeToQuoted = e.Value;
        }

        private void OnMimeTextChanged(object sender, TextChangedEventArgs e)
        {
            _mimeType = e.NewTextValue;
        }

        private void OnIsCusomtMimeChanged(object sender, CheckedChangedEventArgs e)
        {
            _isCustomMime = e.Value;
        }
    }
}
