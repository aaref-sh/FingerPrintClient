using FingerPrintClient.FP.Utilities;
using libzkfpcsharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;

namespace FingerPrintClient.FP;

public enum CaptureMode
{
    IDENTIFICATION,
    REGISTRATION,
    PHOTO
}

public class FPScanner
{
    #region Events
    event EventHandler<InternalCaptureEventArgs> Capture;

    public event EventHandler<ErrorEventArgs> OnIdentifyError;
    public event EventHandler<IdentifyEventArgs> OnIdentifySuccess;

    public event EventHandler<ErrorEventArgs> OnRegisterError;
    public event EventHandler<RegisterProgressEventArgs> OnRegisterProgress;
    public event EventHandler<RegisterEventArgs> OnRegisterSuccess;

    public event EventHandler<PhotoEventArgs> OnPhoto;
    #endregion

    #region Properties
    public CaptureMode Mode { get; set; }
    public int RegistrationCount => regCount;
    public Size ImageSize => new Size(_imgWidth, _imgHeight);

    /// <summary>
    /// DB of FPs holds only id-template
    /// </summary>
    public Dictionary<int, byte[]> Fingerprints { get; set; } = new Dictionary<int, byte[]>();
    #endregion

    #region fields
    IntPtr _devHandle = IntPtr.Zero;
    IntPtr _cacheHandle = IntPtr.Zero;
    Thread _bgWorker;
    int _imgWidth, _imgHeight;
    bool _timeToDie, _connected;
    #endregion

    public FPScanner()
    {
        Mode = CaptureMode.IDENTIFICATION;
        Fingerprints.Clear();
    }

    public void Start()
    {
        if (_connected)
        {
            return;
        }
        var val = zkfp2.Init();
        if (val == zkfperrdef.ZKFP_ERR_OK)
        {
            int nCount = zkfp2.GetDeviceCount();
            if (nCount <= 0)
            {
                zkfp2.Terminate();
                throw new Exception(FPErrors.DEVICE_NOT_FOUND);
            }
        }
        else
        {
            throw new Exception(FPErrors.GetCodeText(val));
        }

        _devHandle = zkfp2.OpenDevice(0);
        if (IntPtr.Zero == _devHandle)
        {
            throw new Exception(FPErrors.GetCodeText(-3));
        }

        _cacheHandle = zkfp2.DBInit();
        if (IntPtr.Zero == _cacheHandle)
        {
            zkfp2.CloseDevice(_devHandle);
            throw new Exception(FPErrors.GetCodeText(-7));
        }

        byte[] paramValue = new byte[4];

        int size = 4;
        var paramVal = zkfp2.GetParameters(_devHandle, 1, paramValue, ref size);
        if (paramVal != zkfperrdef.ZKFP_ERR_OK)
        {
            throw new Exception(FPErrors.GetCodeText(paramVal));
        }
        zkfp2.ByteArray2Int(paramValue, ref _imgWidth);

        size = 4;
        paramVal = zkfp2.GetParameters(_devHandle, 2, paramValue, ref size);
        if (paramVal != zkfperrdef.ZKFP_ERR_OK)
        {
            throw new Exception(FPErrors.GetCodeText(paramVal));
        }
        zkfp2.ByteArray2Int(paramValue, ref _imgHeight);
        _bgWorker = new Thread(new ThreadStart(DoCapture))
        {
            Priority = ThreadPriority.AboveNormal
        };
        _connected = true;
        _timeToDie = false;
        //_bgWorker.IsBackground = true;
        _bgWorker.Start();
    }

    public void Stop()
    {
        _timeToDie = true;
        try
        {
            Capture -= FPReader_Capture;

            _bgWorker?.Join();
            _bgWorker = null;
            zkfp2.DBClear(_cacheHandle);
            zkfp2.DBFree(_cacheHandle);
            zkfp2.CloseDevice(_devHandle);
            zkfp2.Terminate();
        }
        catch { }
        _connected = false;
    }

    private void DoCapture()
    {
        this.Capture += FPReader_Capture;
        while (!_timeToDie)
        {
            CaptureFingerprint();
            Thread.Sleep(200);
        }
    }

    private void CaptureFingerprint()
    {
        int captureTmplateLength = 2048;
        var tempTemplateBuffer = new byte[captureTmplateLength];
        var tempImageBuffer = new byte[_imgWidth * _imgHeight];
        int ret = zkfp2.AcquireFingerprint(_devHandle, tempImageBuffer, tempTemplateBuffer, ref captureTmplateLength);
        if (ret == zkfperrdef.ZKFP_ERR_OK)
        {
            Capture?.Invoke(this, new InternalCaptureEventArgs(tempTemplateBuffer, tempImageBuffer));
        }
        //else
        //{
        //    if (ret != -8)
        //        OnError?.Invoke(ret, new ErrorEventArgs { Message = Errors.GetCodeText(ret) });
        //}
    }

    private void FPReader_Capture(object sender, InternalCaptureEventArgs e)
    {
        if (Mode == CaptureMode.IDENTIFICATION)
        {
            Identify(e.Template, e.Image);
        }
        else if (Mode == CaptureMode.REGISTRATION)
        {
            Register(e.Template, e.Image);
        }
        else
        {
            GetImage(e.Image);
        }
    }

    int regCount = 0;
    static readonly object regLock = new object();
    byte[][] regTemplates;
    private void Register(byte[] template, byte[] image)
    {
        lock (regLock)
        {
            if (regCount == 0)
            {
                regTemplates = new byte[3][];
                var fingerId = 0;
                try
                {
                    fingerId = Identify(template);
                }
                catch
                {
                    OnRegisterError?.Invoke(this, new ErrorEventArgs(FPErrors.FP_GENERAL_ERROR));
                    return;
                }
                if (fingerId > 0)
                {
                    OnRegisterError?.Invoke(this, new ErrorEventArgs(FPErrors.FP_ALREADY_EXISTS));
                    return;
                }
            }

            regTemplates[regCount] = template;
            if (regCount > 0)
            {
                var score = zkfp2.DBMatch(_cacheHandle, regTemplates[regCount - 1], regTemplates[regCount]);
                if (score < 50)
                {
                    OnRegisterError?.Invoke(this, new ErrorEventArgs(FPErrors.GetCodeText(-20)));
                    return;
                }
            }
            if (regCount == 2)
            {
                int captureTemplateLength = 2048, fid = new Random(DateTime.Now.Millisecond).Next(100000, 500000);

                var finalTemplate = new byte[captureTemplateLength];
                var finalVal = zkfp2.DBMerge(_cacheHandle, regTemplates[0], regTemplates[1], regTemplates[2], finalTemplate, ref captureTemplateLength);
                if (finalVal != zkfperrdef.ZKFP_ERR_OK)
                {
                    OnRegisterError?.Invoke(this, new ErrorEventArgs(FPErrors.GetCodeText(finalVal)));
                    regCount = 0;
                    return;
                }
                var addVal = zkfp2.DBAdd(_cacheHandle, fid, finalTemplate);
                if (addVal != zkfperrdef.ZKFP_ERR_OK)
                {
                    OnRegisterError?.Invoke(this, new ErrorEventArgs(FPErrors.GetCodeText(addVal)));
                    regCount = 0;
                    return;
                }
                zkfp2.DBClear(_cacheHandle);
                regCount = 0;
                OnRegisterSuccess?.Invoke(this, new RegisterEventArgs(finalTemplate, image, _imgWidth, _imgHeight));
                return;
            }
            OnRegisterProgress?.Invoke(this, new RegisterProgressEventArgs(regCount, image, _imgWidth, _imgHeight));
            regCount++;
        }
    }

    static readonly object idLock = new object();
    private void Identify(byte[] template, byte[] image)
    {
        lock (idLock)
        {
            var personId = 0;
            try
            {
                personId = Identify(template);
            }
            catch (Exception ex)
            {
                OnIdentifyError?.Invoke(this, new ErrorEventArgs(ex.Message));
            }
            if (personId > 0)
            {
                OnIdentifySuccess?.Invoke(this, new IdentifyEventArgs(personId, image, _imgWidth, _imgHeight));
            }
            else
            {
                OnIdentifyError?.Invoke(this, new ErrorEventArgs(FPErrors.FP_NOT_FOUND));
            }
        }
    }

    private int Identify(byte[] template)
    {
        if (zkfp2.DBCount(_cacheHandle) != Fingerprints.Count)
        {
            zkfp2.DBClear(_cacheHandle);
            foreach (var finger in Fingerprints)
            {
                var val = zkfp2.DBAdd(_cacheHandle, finger.Key, finger.Value);
                if (val != zkfperrdef.ZKFP_ERR_OK)
                {
                    throw new Exception(FPErrors.GetCodeText(val));
                }
            }
        }

        int fid = 0, score = 0;
        int retVal = zkfp2.DBIdentify(_cacheHandle, template, ref fid, ref score);
        if (retVal == zkfperrdef.ZKFP_ERR_OK)
        {
            return fid;
        }
        return -1;
    }

    private void GetImage(byte[] img)
    {
        OnPhoto?.Invoke(this, new PhotoEventArgs(img, _imgWidth, _imgHeight));
    }
}

#region EventArgs
class InternalCaptureEventArgs : EventArgs
{
    public byte[] Template { get; set; }
    public byte[] Image { get; set; }

    public InternalCaptureEventArgs(byte[] template, byte[] image)
    {
        Image = image;
        Template = template;
    }
}

public class ErrorEventArgs : EventArgs
{
    public string Message { get; private set; }

    public ErrorEventArgs(string message)
    {
        Message = message;
    }
}

public class IdentifyEventArgs : EventArgs
{
    public Image Image => FPImageUtilities.GetImage(ImageBytes, w, h);
    public int FingerprintId { get; private set; }
    public byte[] ImageBytes { get; private set; }
    private readonly int w;
    private readonly int h;

    public IdentifyEventArgs(int personId, byte[] image, int width, int height)
    {
        FingerprintId = personId;
        ImageBytes = image;
        w = width;
        h = height;
    }
}
public class RegisterEventArgs : EventArgs
{
    public Image Image => FPImageUtilities.GetImage(ImageBytes, w, h);
    public byte[] Template { get; private set; }
    public byte[] ImageBytes { get; private set; }
    private readonly int w;
    private readonly int h;

    public RegisterEventArgs(byte[] template, byte[] image, int width, int height)
    {
        Template = template;
        ImageBytes = image;
        w = width;
        h = height;
    }
}

public class RegisterProgressEventArgs : EventArgs
{
    public Image Image => FPImageUtilities.GetImage(ImageBytes, w, h);
    public int Step { get; private set; }
    public byte[] ImageBytes { get; private set; }
    private readonly int w;
    private readonly int h;

    public RegisterProgressEventArgs(int step, byte[] image, int width, int height)
    {
        Step = step;
        ImageBytes = image;
        w = width;
        h = height;
    }
}

public class PhotoEventArgs : EventArgs
{
    public Image Image => FPImageUtilities.GetImage(ImageBytes, w, h);
    public byte[] ImageBytes { get; private set; }

    private readonly int w;
    private readonly int h;

    public PhotoEventArgs(byte[] image, int width, int height)
    {
        ImageBytes = image;
        w = width;
        h = height;
    }
}
#endregion
