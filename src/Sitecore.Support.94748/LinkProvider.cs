namespace Sitecore.Links
{
  using Sitecore;
  using Sitecore.Configuration;
  using Sitecore.Data;
  using Sitecore.Data.Items;
  using Sitecore.Data.Managers;
  using Sitecore.Diagnostics;
  using Sitecore.Globalization;
  using Sitecore.IO;
  using Sitecore.Resources.Media;
  using Sitecore.SecurityModel;
  using Sitecore.Sites;
  using Sitecore.StringExtensions;
  using Sitecore.Web;
  using System;
  using System.Collections.Generic;
  using System.Collections.ObjectModel;
  using System.Collections.Specialized;
  using System.Configuration.Provider;
  using System.Runtime.CompilerServices;
  using System.Web;
  using System.Reflection;

  public class LinkProvider : ProviderBase
  {
    public class LinkBuilder
    {
      private readonly UrlOptions _options;
      private static Dictionary<SiteKey, SiteInfo> _siteResolvingTable;
      private static List<SiteInfo> _sites;
      private static readonly object _syncRoot = new object();

      public LinkBuilder(UrlOptions options)
      {
        Assert.ArgumentNotNull(options, "options");
        this._options = options;
      }

      protected virtual string BuildItemUrl(Item item)
      {
        Assert.ArgumentNotNull(item, "item");
        SiteInfo site = this.ResolveTargetSite(item);
        string itemPathElement = this.GetItemPathElement(item, site);
        if (itemPathElement.Length == 0)
        {
          return string.Empty;
        }
        string serverUrlElement = this.GetServerUrlElement(site);
        if (site != null)
        {
          return this.BuildItemUrl(serverUrlElement, itemPathElement, site.VirtualFolder);
        }
        return this.BuildItemUrl(serverUrlElement, itemPathElement);
      }

      protected virtual string BuildItemUrl(string serverUrl, string itemPath)
      {
        Assert.ArgumentNotNull(serverUrl, "serverUrl");
        Assert.ArgumentNotNull(itemPath, "itemPath");
        return this.BuildItemUrl(serverUrl, itemPath, "/");
      }

      protected virtual string BuildItemUrl(string serverUrl, string itemPath, string virtualFolder)
      {
        Assert.ArgumentNotNull(serverUrl, "serverUrl");
        Assert.ArgumentNotNull(itemPath, "itemPath");
        Assert.ArgumentNotNull(virtualFolder, "virtualFolder");
        string str = string.Empty;
        if (!serverUrl.EndsWith("/", StringComparison.InvariantCulture))
        {
          str = str + '/';
        }
        bool flag = this.EmbedLanguage();
        if (flag && (this._options.LanguageLocation == LanguageLocation.FilePath))
        {
          str = FileUtil.MakePath(str, this._options.Language.Name, '/');
        }
        str = FileUtil.MakePath(str, itemPath, '/');
        if (str.Length > 1)
        {
          str = StringUtil.RemovePostfix('/', str);
        }
        if (this._options.EncodeNames)
        {
          str = MainUtil.EncodePath(str, '/');
        }
        if ((this.AddAspxExtension && (str != StringUtil.RemovePostfix('/', virtualFolder))) && ((StringUtil.RemovePostfix('/', str) != StringUtil.RemovePostfix('/', serverUrl)) && (str != "/")))
        {
          str = str + '.' + "aspx";
        }
        if (flag && (this._options.LanguageLocation == LanguageLocation.QueryString))
        {
          str = str + "?sc_lang=" + this._options.Language.Name;
        }
        return (serverUrl + str);
      }

      protected static SiteKey BuildKey(string path, string language)
      {
        Assert.ArgumentNotNull(path, "path");
        Assert.ArgumentNotNull(language, "language");
        if (!Settings.Rendering.SiteResolvingMatchCurrentLanguage)
        {
          language = string.Empty;
        }
        else if (string.IsNullOrEmpty(language) && (LanguageManager.DefaultLanguage != null))
        {
          language = LanguageManager.DefaultLanguage.Name;
        }
        return new SiteKey(path.ToLowerInvariant(), language);
      }

      protected Dictionary<SiteKey, SiteInfo> BuildSiteResolvingTable(IEnumerable<SiteInfo> sites)
      {
        Assert.ArgumentNotNull(sites, "sites");
        Dictionary<SiteKey, SiteInfo> dictionary = new Dictionary<SiteKey, SiteInfo>();
        KeyGetter[] getterArray = new KeyGetter[] { info => BuildKey(FileUtil.MakePath(info.RootPath, info.StartItem).ToLowerInvariant(), info.Language) };
        foreach (KeyGetter getter in getterArray)
        {
          foreach (SiteInfo info in sites)
          {
            if (!this.SiteCantBeResolved(info))
            {
              SiteKey key = getter(info);
              if (!dictionary.ContainsKey(key))
              {
                dictionary.Add(key, info);
              }
            }
          }
        }
        return dictionary;
      }

      protected virtual bool EmbedLanguage()
      {
        if (this._options.LanguageEmbedding == LanguageEmbedding.Always)
        {
          return true;
        }
        if (this._options.LanguageEmbedding == LanguageEmbedding.Never)
        {
          return false;
        }
        SiteContext site = Context.Site;
        return ((site == null) || ((WebUtil.GetOriginalCookieValue(site.GetCookieKey("lang")) == null) || this._options.EmbedLanguage(Context.Language)));
      }

      protected static SiteInfo FindMatchingSite(Dictionary<SiteKey, SiteInfo> resolvingTable, SiteKey key)
      {
        Assert.ArgumentNotNull(resolvingTable, "resolvingTable");
        Assert.ArgumentNotNull(key, "key");
        if (key.Language.Length == 0)
        {
          return FindMatchingSiteByPath(resolvingTable, key.Path);
        }
        Label_0030:
        if (resolvingTable.ContainsKey(key))
        {
          return resolvingTable[key];
        }
        int length = key.Path.LastIndexOf("/", StringComparison.InvariantCulture);
        if (length > 1)
        {
          key = BuildKey(key.Path.Substring(0, length), key.Language);
          goto Label_0030;
        }
        return null;
      }

      protected static SiteInfo FindMatchingSiteByPath(Dictionary<SiteKey, SiteInfo> resolvingTable, string path)
      {
        Assert.ArgumentNotNull(resolvingTable, "resolvingTable");
        Assert.ArgumentNotNull(path, "path");
        Label_0016:
        foreach (KeyValuePair<SiteKey, SiteInfo> pair in resolvingTable)
        {
          SiteInfo info = pair.Value;
          if (pair.Key.Path.Equals(path, StringComparison.InvariantCultureIgnoreCase))
          {
            return info;
          }
        }
        int length = path.LastIndexOf("/", StringComparison.InvariantCulture);
        if (length > 1)
        {
          path = path.Substring(0, length);
          goto Label_0016;
        }
        return null;
      }

      internal virtual string GetHostName() =>
          WebUtil.GetHostName();

      protected virtual string GetItemPathElement(Item item, SiteInfo site)
      {
        ItemPathType type = this.UseDisplayName ? ItemPathType.DisplayName : ItemPathType.Name;
        string path = item.Paths.GetPath(type);
        if (site != null)
        {
          string rootPath = this.GetRootPath(site, item.Language, item.Database).Trim().TrimEnd(new char[] { '/' });
          if (this.IsDescendantOrSelfOf(path, rootPath))
          {
            path = path.Substring(rootPath.Length);
          }
          else
          {
            rootPath = this.GetRootPath(site, item.Language, item.Database, false).Trim().TrimEnd(new char[] { '/' });
            if (this.IsDescendantOrSelfOf(path, rootPath))
            {
              path = path.Substring(rootPath.Length);
            }
          }
          string virtualFolder = site.VirtualFolder;
          if ((virtualFolder.Length > 0) && !path.StartsWith(virtualFolder, StringComparison.OrdinalIgnoreCase))
          {
            path = FileUtil.MakePath(virtualFolder, path);
          }
        }
        return path;
      }

      public string GetItemUrl(Item item)
      {
        Assert.ArgumentNotNull(item, "item");
        return this.BuildItemUrl(item);
      }

      protected virtual string GetRootPath(SiteInfo site, Language language, Database database) =>
          this.GetRootPath(site, language, database, true);

      protected virtual string GetRootPath(SiteInfo site, Language language, Database database, bool useStartItem)
      {
        string itemPath = useStartItem ? FileUtil.MakePath(site.RootPath, site.StartItem) : site.RootPath;
        if (!this.UseDisplayName)
        {
          return itemPath;
        }
        if (itemPath.Length == 0)
        {
          return string.Empty;
        }
        Item item = ItemManager.GetItem(itemPath, language, Sitecore.Data.Version.Latest, database, SecurityCheck.Disable);
        if (item == null)
        {
          return string.Empty;
        }
        return item.Paths.GetPath(ItemPathType.DisplayName);
      }

      internal virtual string GetScheme() =>
          WebUtil.GetScheme();

      protected virtual string GetServerUrlElement(SiteInfo siteInfo)
      {
        SiteContext site = Context.Site;
        string str = (site != null) ? site.Name : string.Empty;
        string hostName = this.GetHostName();
        string str3 = this.AlwaysIncludeServerUrl ? WebUtil.GetServerUrl() : string.Empty;
        if (siteInfo == null)
        {
          return str3;
        }
        string str4 = ((!string.IsNullOrEmpty(siteInfo.HostName) && !string.IsNullOrEmpty(hostName)) && siteInfo.Matches(hostName)) ? hostName : StringUtil.GetString(new string[] { this.GetTargetHostName(siteInfo), hostName });
        if ((!this.AlwaysIncludeServerUrl && siteInfo.Name.Equals(str, StringComparison.OrdinalIgnoreCase)) && hostName.Equals(str4, StringComparison.OrdinalIgnoreCase))
        {
          return str3;
        }
        if ((str4 == string.Empty) || (str4.IndexOf('*') >= 0))
        {
          return str3;
        }
        string str5 = StringUtil.GetString(new string[] { siteInfo.Scheme, this.GetScheme() });
        int @int = MainUtil.GetInt(siteInfo.Port, WebUtil.GetPort());
        int port = WebUtil.GetPort();
        string scheme = this.GetScheme();
        StringComparison ordinalIgnoreCase = StringComparison.OrdinalIgnoreCase;
        if ((str4.Equals(hostName, ordinalIgnoreCase) && (@int == port)) && str5.Equals(scheme, ordinalIgnoreCase))
        {
          return str3;
        }
        string str7 = str5 + "://" + str4;
        if ((@int > 0) && (@int != 80))
        {
          str7 = str7 + ":" + @int;
        }
        return str7;
      }

      protected Dictionary<SiteKey, SiteInfo> GetSiteResolvingTable()
      {
        List<SiteInfo> sites = SiteContextFactory.Sites;
        if (!object.ReferenceEquals(_sites, sites))
        {
          lock (_syncRoot)
          {
            if (!object.ReferenceEquals(_sites, sites))
            {
              _sites = sites;
              _siteResolvingTable = null;
            }
          }
        }
        if (_siteResolvingTable == null)
        {
          lock (_syncRoot)
          {
            if (_siteResolvingTable == null)
            {
              _siteResolvingTable = this.BuildSiteResolvingTable(_sites);
            }
          }
        }
        return _siteResolvingTable;
      }

      protected virtual string GetTargetHostName(SiteInfo siteInfo)
      {
        Assert.ArgumentNotNull(siteInfo, "siteInfo");
        if (!siteInfo.TargetHostName.IsNullOrEmpty())
        {
          return siteInfo.TargetHostName;
        }
        string hostName = siteInfo.HostName;
        if (hostName.IndexOfAny(new char[] { '*', '|' }) < 0)
        {
          return hostName;
        }
        return string.Empty;
      }

      public virtual SiteInfo GetTargetSite(Item item)
      {
        Assert.ArgumentNotNull(item, "item");
        return this.ResolveTargetSite(item);
      }

      protected virtual bool IsDescendantOrSelfOf(string itemPath, string rootPath)
      {
        Assert.ArgumentNotNull(itemPath, "itemPath");
        Assert.ArgumentNotNull(rootPath, "rootPath");
        if (!string.IsNullOrEmpty(rootPath) && itemPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
          if (string.Compare(itemPath, rootPath, StringComparison.OrdinalIgnoreCase) == 0)
          {
            return true;
          }
          if (itemPath.StartsWith(rootPath + "/", StringComparison.OrdinalIgnoreCase))
          {
            return true;
          }
        }
        return false;
      }

      protected virtual bool MatchCurrentSite(Item item, SiteContext currentSite)
      {
        Assert.ArgumentNotNull(item, "item");
        Assert.ArgumentNotNull(currentSite, "currentSite");
        if (!Settings.Rendering.SiteResolvingMatchCurrentSite)
        {
          return false;
        }
        if (Settings.Rendering.SiteResolvingMatchCurrentLanguage && !item.Language.ToString().Equals(currentSite.Language, StringComparison.InvariantCultureIgnoreCase))
        {
          return false;
        }
        string fullPath = item.Paths.FullPath;
        string startPath = currentSite.StartPath;
        if (!fullPath.StartsWith(startPath, StringComparison.InvariantCultureIgnoreCase))
        {
          return false;
        }
        if ((fullPath.Length > startPath.Length) && (fullPath[startPath.Length] != '/'))
        {
          return false;
        }
        return true;
      }

      protected virtual SiteInfo ResolveTargetSite(Item item)
      {
        SiteContext site = Context.Site;
        SiteContext currentSite = this._options.Site ?? site;
        SiteInfo info = (currentSite != null) ? currentSite.SiteInfo : null;
        if (this._options.SiteResolving && (item.Database.Name != "core"))
        {
          if ((this._options.Site != null) && ((site == null) || (this._options.Site.Name != site.Name)))
          {
            return info;
          }
          if ((currentSite == null) || !this.MatchCurrentSite(item, currentSite))
          {
            Dictionary<SiteKey, SiteInfo> siteResolvingTable = this.GetSiteResolvingTable();
            string path = item.Paths.FullPath.ToLowerInvariant();
            SiteInfo info2 = FindMatchingSite(siteResolvingTable, BuildKey(path, item.Language.ToString())) ?? FindMatchingSiteByPath(siteResolvingTable, path);
            if (info2 != null)
            {
              return info2;
            }
          }
        }
        return info;
      }

      protected virtual bool SiteCantBeResolved(SiteInfo siteInfo)
      {
        Assert.ArgumentNotNull(siteInfo, "siteInfo");
        if (!string.IsNullOrEmpty(siteInfo.HostName) && string.IsNullOrEmpty(this.GetTargetHostName(siteInfo)))
        {
          Log.Warn("LinkBuilder. Site '{0}' should have defined 'targethostname' property in order to take participation in site resolving process.".FormatWith(new object[] { siteInfo.Name }), typeof(LinkProvider.LinkBuilder));
          return true;
        }
        if (!string.IsNullOrEmpty(this.GetTargetHostName(siteInfo)) && !string.IsNullOrEmpty(siteInfo.RootPath))
        {
          return string.IsNullOrEmpty(siteInfo.StartItem);
        }
        return true;
      }

      protected bool AddAspxExtension =>
          this._options.AddAspxExtension;

      protected bool AlwaysIncludeServerUrl =>
          this._options.AlwaysIncludeServerUrl;

      protected bool UseDisplayName =>
          this._options.UseDisplayName;

      private delegate LinkProvider.LinkBuilder.SiteKey KeyGetter(SiteInfo siteInfo);

      protected class SiteKey
      {
        public SiteKey(string path, string language)
        {
          Assert.ArgumentNotNull(path, "path");
          Assert.ArgumentNotNull(language, "language");
          this.Path = path;
          this.Language = language;
        }

        public override bool Equals(object obj)
        {
          if (obj == null)
          {
            return false;
          }
          if (object.ReferenceEquals(this, obj))
          {
            return true;
          }
          if (!(obj is LinkProvider.LinkBuilder.SiteKey))
          {
            return false;
          }
          LinkProvider.LinkBuilder.SiteKey key = obj as LinkProvider.LinkBuilder.SiteKey;
          return (this.Language.Equals(key.Language) && this.Path.Equals(key.Path, StringComparison.InvariantCultureIgnoreCase));
        }

        public override int GetHashCode() =>
            (this.Path.GetHashCode() ^ this.Language.GetHashCode());

        public string Language { get; private set; }

        public string Path { get; private set; }
      }
    }

  }
}
