namespace Sitecore.Support.Links
{
  using Sitecore;
  using Sitecore.Data.Items;
  using Sitecore.Diagnostics;
  using Sitecore.Links;
  using Sitecore.Sites;
  using Sitecore.Web;
  using System;

  public class LinkProvider : Sitecore.Links.LinkProvider
  {
    protected override Sitecore.Links.LinkProvider.LinkBuilder CreateLinkBuilder(UrlOptions options) =>
        new LinkBuilderSupport(options);

    public override string GetItemUrl(Item item, UrlOptions options)
    {
      Assert.ArgumentNotNull(item, "item");
      Assert.ArgumentNotNull(options, "options");
      string itemUrl = ((LinkBuilderSupport)this.CreateLinkBuilder(options)).GetItemUrl(item);
      if (this.LowercaseUrls)
      {
        itemUrl = itemUrl.ToLowerInvariant();
      }
      return itemUrl;
    }

    public class LinkBuilderSupport : Sitecore.Links.LinkProvider.LinkBuilder
    {
      public LinkBuilderSupport(UrlOptions options) : base(options)
      {
      }

      internal virtual string GetHostName() =>
          WebUtil.GetHostName();

      internal virtual string GetScheme() =>
          WebUtil.GetScheme();

      protected override string GetServerUrlElement(SiteInfo siteInfo)
      {
        SiteContext site = Context.Site;
        string str = (site != null) ? site.Name : string.Empty;
        string hostName = this.GetHostName();
        string str3 = base.AlwaysIncludeServerUrl ? WebUtil.GetServerUrl() : string.Empty;
        if (siteInfo == null)
        {
          return str3;
        }
        string str4 = ((!string.IsNullOrEmpty(siteInfo.HostName) && !string.IsNullOrEmpty(hostName)) && siteInfo.Matches(hostName)) ? hostName : StringUtil.GetString(new string[] { this.GetTargetHostName(siteInfo), hostName });
        //The fix
        if (!(base.AlwaysIncludeServerUrl || (!siteInfo.Name.Equals(str, StringComparison.OrdinalIgnoreCase) && !hostName.Equals(str4, StringComparison.OrdinalIgnoreCase))))
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
    }
  }
}
