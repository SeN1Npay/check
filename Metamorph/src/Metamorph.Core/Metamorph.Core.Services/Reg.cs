using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace Metamorph.Core.Services;

public static class Reg
{
	public static void SetDword(string key, string name, uint value)
	{
		using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(key, writable: true);
		registryKey.SetValue(name, (int)value, RegistryValueKind.DWord);
	}

	public static void SetDword(string key, string name, bool value)
	{
		SetDword(key, name, value ? 1u : 0u);
	}

	public static void SetString(string key, string name, string value)
	{
		using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(key, writable: true);
		registryKey.SetValue(name, value, RegistryValueKind.String);
	}

	public static void SetExpandString(string key, string name, string value)
	{
		using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(key, writable: true);
		registryKey.SetValue(name, value, RegistryValueKind.ExpandString);
	}

	public static IReadOnlyList<string> SubKeys(string key)
	{
		using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(key);
		return registryKey?.GetSubKeyNames() ?? Array.Empty<string>();
	}

	public static IReadOnlyList<string> ValueNames(string key)
	{
		using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(key);
		return registryKey?.GetValueNames() ?? Array.Empty<string>();
	}

	public static void SetBinary(string key, string name, byte[] value)
	{
		using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(key, writable: true);
		registryKey.SetValue(name, value, RegistryValueKind.Binary);
	}

	public static void Delete(string key, string name)
	{
		using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(key, writable: true);
		registryKey?.DeleteValue(name, throwOnMissingValue: false);
	}

	public static object? Get(string key, string name)
	{
		using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(key);
		return registryKey?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
	}

	public static uint? GetDword(string key, string name)
	{
		object obj = Get(key, name);
		if (!(obj is int))
		{
			return null;
		}
		return (uint)(int)obj;
	}

	public static RegistryValueKind? GetKind(string key, string name)
	{
		using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(key);
		if (registryKey == null || registryKey.GetValue(name) == null)
		{
			return null;
		}
		return registryKey.GetValueKind(name);
	}
}
