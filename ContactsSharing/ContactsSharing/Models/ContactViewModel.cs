using System.ComponentModel;
using System.Runtime.CompilerServices;
using NickBuhro.Translit;
using Plugin.ContactService.Shared;
using Xamarin.Forms;

namespace ContactsSharing.Models
{
    public class ContactViewModel : INotifyPropertyChanged
    {
        public readonly string OriginalName;

        public ContactViewModel(Contact contact)
        {
            Contact = contact;
            OriginalName = contact.Name;
            Name = contact.Name;
        }
        public ContactViewModel()
        {

        }

        private Contact _contact;
        public Contact Contact
        {
            get => _contact;
            set
            {
                _contact = value;
                OnPropertyChanged();
            }
        }
        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }


        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        private bool _transliterated;
        public bool Transliterated
        {
            get => _transliterated;
            set
            {
                _transliterated = value;
                OnPropertyChanged();
            }
        }
        private Command _transliterateCommand;

        public Command TransliterateCommand => _transliterateCommand ??= new Command(OnTransliterate);
        private void OnTransliterate()
        {
            Transliterated = !Transliterated;
            Name = Transliterated ? Transliteration.CyrillicToLatin(Contact.Name, Language.Russian) : OriginalName;
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
