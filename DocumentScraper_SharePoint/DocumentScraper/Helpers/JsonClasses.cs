using System;
using System.Collections.Generic;

namespace DocumentScraper.Helpers
{ 
    public class Metadata
    {
        public string id { get; set; }
        public string uri { get; set; }
        public string type { get; set; }
    }

    public class Deferred
    {
        public string uri { get; set; }
    }

    public class Alerts
    {
        public Deferred __deferred { get; set; }
    }

    public class Groups
    {
        public object __deferred { get; set; }
    }

    public class UserId
    {
        public object __metadata { get; set; }
        public string NameId { get; set; }
        public string NameIdIssuer { get; set; }
    }

    public class Author
    {
        public object __metadata { get; set; }
        public Alerts Alerts { get; set; }
        public Groups Groups { get; set; }
        public int Id { get; set; }
        public bool IsHiddenInUI { get; set; }
        public string LoginName { get; set; }
        public string Title { get; set; }
        public int PrincipalType { get; set; }
        public string Email { get; set; }
        public bool IsShareByEmailGuestUser { get; set; }
        public bool IsSiteAdmin { get; set; }
        public UserId UserId { get; set; }
    }

    public class CheckedOutByUser
    {
        public object __deferred { get; set; }
    }

    public class EffectiveInformationRightsManagementSettings
    {
        public object __deferred { get; set; }
    }

    public class InformationRightsManagementSettings
    {
        public object __deferred { get; set; }
    }

    public class ListItemAllFields
    {
        public object __deferred { get; set; }
    }

    public class LockedByUser
    {
        public object __deferred { get; set; }
    }

    public class ModifiedBy
    {
        public object __deferred { get; set; }
    }

    public class Properties
    {
        public object __deferred { get; set; }
    }

    public class VersionEvents
    {
        public object __deferred { get; set; }
    }

    public class Versions
    {
        public object __deferred { get; set; }
    }

    public class Result
    {
        public Metadata __metadata { get; set; }
        public Author Author { get; set; }
        public CheckedOutByUser CheckedOutByUser { get; set; }
        public EffectiveInformationRightsManagementSettings EffectiveInformationRightsManagementSettings { get; set; }
        public InformationRightsManagementSettings InformationRightsManagementSettings { get; set; }
        public ListItemAllFields ListItemAllFields { get; set; }
        public LockedByUser LockedByUser { get; set; }
        public ModifiedBy ModifiedBy { get; set; }
        public Properties Properties { get; set; }
        public VersionEvents VersionEvents { get; set; }
        public Versions Versions { get; set; }
        public string CheckInComment { get; set; }
        public int CheckOutType { get; set; }
        public string ContentTag { get; set; }
        public int CustomizedPageStatus { get; set; }
        public string ETag { get; set; }
        public bool Exists { get; set; }
        public bool IrmEnabled { get; set; }
        public string Length { get; set; }
        public int Level { get; set; }
        public string LinkingUri { get; set; }
        public string LinkingUrl { get; set; }
        public int MajorVersion { get; set; }
        public int MinorVersion { get; set; }
        public string Name { get; set; }
        public string ServerRelativeUrl { get; set; }
        public DateTime TimeCreated { get; set; }
        public DateTime TimeLastModified { get; set; }
        public string Title { get; set; }
        public int UIVersion { get; set; }
        public string UIVersionLabel { get; set; }
        public string UniqueId { get; set; }
    }

    public class D
    {
        public IList<Result> Results { get; set; }
    }

    public class RootObject
    {
        public D d { get; set; }
    }
}
