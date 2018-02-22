using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spi
{
    public static class ListExtension
    {
        public static void AddSorted<T>(this List<T> list, T value, IComparer<T> comparer)
        {
            int x = list.BinarySearch(value, comparer);
            list.Insert( 
                index:  x >= 0 ? x : ~x, 
                item:   value);
        }
    }
}
