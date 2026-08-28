using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LicenseManager.Core.DTOs;

namespace LicenseManager.Client.Models;

public enum LicenseFetchStatus
{
    Success,
    Rejected,
    Unavailable
}

public class LicenseFetchResult
{
    public LicenseFetchStatus Status { get; set; }

    public SignedLicenseResponse? License { get; set; }
}