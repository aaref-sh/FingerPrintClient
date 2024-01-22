using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FingerPrintClient.Fingerprint;

enum FPParameters
{
    ImageWidth = 1,
    ImageHeight = 2,
    ImageDPI = 3,
    ImageDataSize = 106,
    AntiFakeFunction = 2002,
    VendorInformation = 1101,
    ProductName = 1102,
    DeviceSerialNumber = 1103,

    BlinkWhiteLight = 101,
    BlinkGreenLight = 102,
    BlinkRedLight = 103,
    Buzz = 104,
}

public class FPParameterReader
{
    IntPtr _deviceHandle;
    public FPParameterReader(IntPtr devHandle)
    {
        _deviceHandle = devHandle;
    }

    void SetParameter(FPParameters fpParam, int val)
    {
    }
}
