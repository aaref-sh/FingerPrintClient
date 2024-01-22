using System;

namespace FingerPrintClient.FP.Utilities;

public class FPSound
{
    public static void BeepError()
    {
        Console.Beep(1000, 200);
    }
    public static void BeepSuccess()
    {
        Console.Beep(5000, 100);
    }
}
