using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace DanSerialiserAnalyser
{
	internal static class NamespaceConfirming
	{
		public static bool IsIn(ISymbol symbol, string ns)
		{
			if (symbol == null)
				throw new ArgumentNullException(nameof(symbol));
			if (string.IsNullOrWhiteSpace(ns))
				throw new ArgumentException($"Null/blank {nameof(ns)} specified");

			var namespaceSegments = new List<string>();
			var containingNamespace = symbol.ContainingNamespace;
			while (!string.IsNullOrEmpty(containingNamespace?.Name))
			{
				namespaceSegments.Insert(0, containingNamespace.Name);
				containingNamespace = containingNamespace.ContainingNamespace;
			}
			return (string.Join(".", namespaceSegments) == ns);
		}
	}
}