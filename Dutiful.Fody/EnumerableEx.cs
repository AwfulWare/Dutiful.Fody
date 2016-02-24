using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

static class EnumerableEx
{
    public static bool SequenceEqual<T>(this IEnumerable<T> self, IEnumerable<T> that, Func<T, T, bool> test)
    {
        var selfCollection = self as ICollection<T>;
        var thatCollection = that as ICollection<T>;
        if (selfCollection != null && thatCollection != null
            && selfCollection.Count != thatCollection.Count)
        {
            return false;
        }

        var selfEnumerator = self.GetEnumerator();
        var thatEnumerator = that.GetEnumerator();
        while (selfEnumerator.MoveNext())
        {
            if (!thatEnumerator.MoveNext())
                return false;

            if (!test(selfEnumerator.Current, thatEnumerator.Current))
                return false;
        }
        return !thatEnumerator.MoveNext();
    }
}
