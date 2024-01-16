using System.Runtime.InteropServices;

namespace MySQL_DB2Code_WPF;

public class FileType
{
	[DllImport("urlmon.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = false)]
	private static extern int FindMimeFromData(IntPtr pBc,
		[MarshalAs(UnmanagedType.LPWStr)] string pwzUrl,
		[MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.I1, SizeParamIndex = 3)]
		byte[] pBuffer,
		int cbSize,
		[MarshalAs(UnmanagedType.LPWStr)] string pwzMimeProposed,
		int dwMimeFlags,
		out IntPtr ppwzMimeOut,
		int dwReserved
	);

	/**
	 * This function will detect mime type from provided byte array
	 * and if it fails, it will return default mime type
	 */
	public static string GetMimeFromBytes(byte[] dataBytes, string defaultMimeType)
	{
		if (dataBytes == null) throw new ArgumentNullException(nameof(dataBytes));

		var mimeType = string.Empty;
		IntPtr suggestPtr = IntPtr.Zero, filePtr = IntPtr.Zero;

		try
		{
			var ret = FindMimeFromData(IntPtr.Zero, null, dataBytes, dataBytes.Length, null, 0, out var outPtr, 0);
			if (ret == 0 && outPtr != IntPtr.Zero)
			{
				mimeType = Marshal.PtrToStringUni(outPtr);
				Marshal.FreeCoTaskMem(outPtr);
			}
		}
		catch
		{
			mimeType = defaultMimeType;
		}

		return mimeType;
	}
}