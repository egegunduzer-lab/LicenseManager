using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LicenseManager.Core.DTOs;

namespace LicenseManager.Client.Services;

public interface ILicenseVerificationService
{
    bool Verify(
        LicensePayload body,
        string signatureBase64);
}
