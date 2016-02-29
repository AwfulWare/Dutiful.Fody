using System;
using System.Collections.Generic;

static partial class EnumerableEx
{
    public static bool All<T>(this IEnumerable<T> source)
    {
        foreach (var item in source) ;
        return true;
    }

    public static bool All<T>(this IEnumerable<T> source, bool errorToFalse)
    {
        try
        {
            foreach (var item in source) ;
        }
        catch
        {
            if (errorToFalse)
                return false;
            throw;
        }
        return true;
    }
    public static bool All<T>(this IEnumerable<T> source, Func<T, bool> test, bool errorToFalse)
    {
        try
        {
            foreach (var item in source)
            {
                if (!test(item))
                    return false;
            }
        }
        catch
        {
            if (errorToFalse)
                return false;
            throw;
        }
        return true;
    }

    public static bool Any<T>(this IEnumerable<T> source, bool errorToFalse)
    {
        try
        {
            foreach (var item in source)
                return true;
        }
        catch
        {
            if (errorToFalse)
                return false;
            throw;
        }
        return false;
    }
    public static bool Any<T>(this IEnumerable<T> source, Func<T, bool> test, bool errorToFalse)
    {
        try
        {
            foreach (var item in source)
            {
                if (test(item))
                    return true;
            }
        }
        catch
        {
            if (errorToFalse)
                return false;
            throw;
        }
        return false;
    }

    public static IEnumerable<TResult> ZipFull<T1st, T2nd, TResult>(this IEnumerable<T1st> src1st, IEnumerable<T2nd> src2nd, Func<T1st, T2nd, TResult> selector)
    {
        if (src1st == null)
            throw new NullReferenceException();
        if (src2nd == null)
            throw new ArgumentNullException(nameof(src2nd));
        if (selector == null)
            throw new ArgumentNullException(nameof(selector));

        var coll1st = src1st as ICollection<T1st>;
        var coll2nd = src2nd as ICollection<T2nd>;
        if (coll1st != null && coll2nd != null
            && coll1st.Count != coll2nd.Count)
        {
            throw new InvalidOperationException();
        }

        var enmtr1st = src1st.GetEnumerator();
        var enmtr2nd = src2nd.GetEnumerator();
        while (enmtr1st.MoveNext())
        {
            if (!enmtr2nd.MoveNext())
                throw new InvalidOperationException();

            yield return selector(enmtr1st.Current, enmtr2nd.Current);
        }
        if (enmtr2nd.MoveNext())
            throw new InvalidOperationException();
    }

    public static bool SequenceEqual<T>(this IEnumerable<T> src1st, IEnumerable<T> src2nd, Func<T, T, bool> test)
    {
        if (src1st == null)
            throw new NullReferenceException();
        if (src2nd == null)
            throw new ArgumentNullException(nameof(src2nd));

        var coll1st = src1st as ICollection<T>;
        var coll2nd = src2nd as ICollection<T>;
        if (coll1st != null && coll2nd != null
            && coll1st.Count != coll2nd.Count)
        {
            return false;
        }

        if (test == null)
            test = EqualityComparer<T>.Default.Equals;

        var enmtr1st = src1st.GetEnumerator();
        var enmtr2nd = src2nd.GetEnumerator();
        while (enmtr1st.MoveNext())
        {
            if (!enmtr2nd.MoveNext())
                return false;

            if (!test(enmtr1st.Current, enmtr2nd.Current))
                return false;
        }
        return !enmtr2nd.MoveNext();
    }
}
