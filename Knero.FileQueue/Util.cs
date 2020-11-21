namespace Knero.FileQueue
{
    public static class Util
    {
        public static bool CompareBytes(byte[] source, int sourceOffset, byte[] pivot)
        {
            for (int i = sourceOffset; i < pivot.Length; ++i)
            {
                if (source[i] != pivot[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
