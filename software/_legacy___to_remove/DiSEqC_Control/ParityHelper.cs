namespace DiSEqC_Control
{
    public class ParityHelper
    {
        public enum Parity
        {
            EVEN = 0,
            ODD = 1
        }
        public static Parity ParityEvenBit(int x)
        {
            var parity = Parity.EVEN;
            var temp = x;

            while (temp > 0)
            {
                parity = (Parity)((int)parity ^ (int)(temp & 0x1));
                temp >>= 1;
            }

            return parity;
        }
    }
}
