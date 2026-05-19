namespace GameDeveloperKit
{
    public static class Crc32Utility
    {
        private static readonly uint[] s_Table = new uint[256];

        static Crc32Utility()
        {
            const uint polynomial = 0xEDB88320;
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) != 0)
                    {
                        crc = (crc >> 1) ^ polynomial;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }

                s_Table[i] = crc;
            }
        }

        public static uint Compute(byte[] data)
        {
            if (data == null)
            {
                return 0;
            }

            uint crc = 0xFFFFFFFF;
            for (int i = 0; i < data.Length; i++)
            {
                var index = (byte)((crc ^ data[i]) & 0xFF);
                crc = (crc >> 8) ^ s_Table[index];
            }

            return crc ^ 0xFFFFFFFF;
        }
    }
}
