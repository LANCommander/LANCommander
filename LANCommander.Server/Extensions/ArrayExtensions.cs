namespace LANCommander.Server.Extensions
{
    public static class ArrayExtensions
    {
        public static T[] ShiftArrayAndInsert<T>(this T[] array, T input, int max)
        {
            if (array == null || array.Length < max)
            {
                array = new T[max];
            }

            Array.Copy(array, 1, array, 0, array.Length - 1);

            array[array.Length - 1] = input;

            return array;
        }
    }
}
