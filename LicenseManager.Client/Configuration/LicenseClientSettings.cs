using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LicenseManager.Client.Configuration;

public class LicenseClientSettings
{
    public string ApiBaseUrl { get; set; } = string.Empty;

    public Guid CompanyId { get; set; }

    public string LicenseKey { get; set; } = string.Empty;

    public string RequiredModule { get; set; } = string.Empty;

    public int RenewalThresholdDays { get; set; } = 7;
}
