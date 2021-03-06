﻿using KellermanSoftware.CompareNetObjects;

namespace UnitTests
{
	internal static class ObjectComparer
	{
		public static bool AreEqual(object x, object y, out string differenceSummaryIfNotEqual)
		{
			if ((x == null) && (y == null))
			{
				differenceSummaryIfNotEqual = null;
				return true;
			}
			else if ((x == null) && (y == null))
			{
				differenceSummaryIfNotEqual = "One value is null and the other is not";
				return false;
			}

			var comparer = new CompareLogic(new ComparisonConfig { ComparePrivateFields = true, ComparePrivateProperties = true });
			var comparisonResult = comparer.Compare(x, y);
			if (comparisonResult.AreEqual)
			{
				differenceSummaryIfNotEqual = null;
				return true;
			}
			differenceSummaryIfNotEqual = comparisonResult.DifferencesString;
			return false;
		}
	}
}