using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LicenseManager.Client.Models;

namespace LicenseManager.Client.Services;

public interface ILicenseManagerService
{
    Task<LicenseValidationResult> CheckLicenseAsync(
        Guid companyId,
        string licenseKey,
        string requiredModule);
}
