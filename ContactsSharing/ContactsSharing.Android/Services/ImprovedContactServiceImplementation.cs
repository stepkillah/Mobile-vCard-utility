using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android.Database;
using Android.Provider;
using ContactsSharing.Droid.Services;
using Plugin.ContactService;
using Plugin.ContactService.Shared;
using Xamarin.Forms;
using Application = Android.App.Application;

[assembly: Dependency(typeof(ImprovedContactServiceImplementation))]
namespace ContactsSharing.Droid.Services
{
    public class ImprovedContactServiceImplementation : IContactService
    {
        private static readonly string[] Projection =
        {
            ContactsContract.Data.InterfaceConsts.Mimetype,
            ContactsContract.Data.InterfaceConsts.ContactId,
            ContactsContract.Contacts.InterfaceConsts.DisplayName,
            ContactsContract.Contacts.InterfaceConsts.PhotoUri,
            ContactsContract.Contacts.InterfaceConsts.PhotoThumbnailUri,
            ContactsContract.CommonDataKinds.Contactables.InterfaceConsts.Data
        };

        private static readonly string Selection = ContactsContract.Data.InterfaceConsts.Mimetype + " in (?, ?)";

        private static readonly string[] SelectionArgs = {
            ContactsContract.CommonDataKinds.Email.ContentItemType,
            ContactsContract.CommonDataKinds.Phone.ContentItemType,
        };

        private const string SortOrder = ContactsContract.Contacts.InterfaceConsts.SortKeyAlternative;

        private Dictionary<string, Contact> FilledContacts { get; } = new Dictionary<string, Contact>();

        private static int _mimeTypeIndex;
        private static int _contactIdIndex;
        private static int _nameIndex;
        private static int _photoUriIndex;
        private static int _photoUriThumbnail;
        private static int _dataIndex;

        public IList<Contact> GetContactList()
        {
            return GetContacts().ToList();
        }


        public Task<IList<Contact>> GetContactListAsync() => Task.Run(() => GetContactList());

        private IEnumerable<Contact> GetContacts(Func<Contact, bool> filter = null)
        {
            var uri = ContactsContract.CommonDataKinds.Contactables.ContentUri;
            var ctx = Application.Context;

            var cursor = ctx.ApplicationContext.ContentResolver.Query(uri, Projection, Selection, SelectionArgs, SortOrder);
            if (cursor.Count == 0) return null;
            _nameIndex = cursor.GetColumnIndex(ContactsContract.Contacts.InterfaceConsts.DisplayName);
            _photoUriIndex = cursor.GetColumnIndex(ContactsContract.Contacts.InterfaceConsts.PhotoUri);
            _photoUriThumbnail = cursor.GetColumnIndex(ContactsContract.Contacts.InterfaceConsts.PhotoThumbnailUri);
            _contactIdIndex = cursor.GetColumnIndex(ContactsContract.Data.InterfaceConsts.ContactId);
            _mimeTypeIndex = cursor.GetColumnIndex(ContactsContract.Data.InterfaceConsts.Mimetype);
            _dataIndex = cursor.GetColumnIndex(ContactsContract.CommonDataKinds.Contactables.InterfaceConsts.Data);
            while (cursor.MoveToNext())
            {
                CreateContact(cursor);
            }

            return FilledContacts.Values.ToList();
        }

        private void CreateContact(ICursor cursor)
        {
            var contactId = GetString(cursor, _contactIdIndex);
            var name = GetString(cursor, _nameIndex);
            Contact contact;
            if (!FilledContacts.ContainsKey(contactId))
            {
                contact = new Contact
                {
                    Name = name,
                    PhotoUri = GetString(cursor, _photoUriIndex),
                    PhotoUriThumbnail = GetString(cursor, _photoUriThumbnail),
                    Emails = new List<string>(),
                    Numbers = new List<string>(),
                };
                FilledContacts[contactId] = contact;
            }
            else
            {
                contact = FilledContacts[contactId];
            }
            string data = GetString(cursor, _dataIndex);
            var mimeType = GetString(cursor, _mimeTypeIndex);
            if (mimeType.Equals(ContactsContract.CommonDataKinds.Email.ContentItemType))
            {
                if (!contact.Emails.Contains(data))
                    contact.Emails.Add(data);
            }
            else
            {
                if (!contact.Numbers.Contains(data))
                    contact.Numbers.Add(data);
            }
        }

        private static string GetString(ICursor cursor, int index)
        {
            return cursor.GetString(index);
        }

    }
}