using System;

namespace KFramework.MonoGame
{
    public static class VersionTool
    {
        public static int GetBigVersionNumber(string versionStr)
        {
            int nNumber = GetVersionArray(versionStr)[0];
            return nNumber;
        }

        public static int VersionCompare(string versionStr1, string versionStr2)
        {
            var array1 = GetVersionArray(versionStr1);
            var array2 = GetVersionArray(versionStr2);

            int nLength = Math.Max(array1.Length, array2.Length);
            for (int i = 0; i < nLength; i++)
            {
                int nNumber1 = GetArrayValue(array1, i);
                int nNumber2 = GetArrayValue(array2, i);
                if (nNumber1 > nNumber2)
                {
                    return 1;
                }
                else if (nNumber1 < nNumber2)
                {
                    return -1;
                }
            }

            return 0;
        }

        private static int GetArrayValue(int[] array, int nIndex)
        {
            if (nIndex >= array.Length)
            {
                return 0;
            }
            else
            {
                return array[nIndex];
            }
        }

        private static int[] GetVersionArray(string versionStr)
        {
            var splitStr = versionStr.Split('.');
            int[] array = new int[splitStr.Length];
            for (int i = 0; i < array.Length; i++)
            {
                int nNumber = int.Parse(splitStr[i]);
                array[i] = nNumber;
            }

            return array;
        }

    }
}
