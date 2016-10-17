﻿using System.Collections.Generic;
using System.Linq;

public static class ListExtensions
{

    public static bool ContainsIndex<T>(this List<T> list, int index)
    {
        return list.ElementAtOrDefault(index) != null;
    }
}
