using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace PdfViewerApp.Services.Cache
{
	public class PdfCacheManager
	{
		private readonly Dictionary<string, BitmapSource> _bitmapCache = new(StringComparer.Ordinal);
		private readonly LinkedList<string> _bitmapCacheOrder = new();
		private readonly Dictionary<string, LinkedListNode<string>> _bitmapCacheNodes = new(StringComparer.Ordinal);
		private long _bitmapCacheBytes;
		private long _maxBitmapCacheBytes = 402653184L;
		private long _cacheHits;
		private long _cacheMisses;

		public long CacheHits
		{
			get
			{
				lock (_bitmapCache)
				{
					return _cacheHits;
				}
			}
		}

		public long CacheMisses
		{
			get
			{
				lock (_bitmapCache)
				{
					return _cacheMisses;
				}
			}
		}

		public double HitRatio
		{
			get
			{
				lock (_bitmapCache)
				{
					long total = _cacheHits + _cacheMisses;
					return total == 0 ? 0.0 : (double)_cacheHits / total * 100.0;
				}
			}
		}

		public PdfCacheManager(long maxCacheBytes)
		{
			_maxBitmapCacheBytes = maxCacheBytes;
		}

		public int Count
		{
			get
			{
				lock (_bitmapCache)
				{
					return _bitmapCache.Count;
				}
			}
		}

		public long Bytes
		{
			get
			{
				lock (_bitmapCache)
				{
					return _bitmapCacheBytes;
				}
			}
		}

		public void SetMaxCacheBytes(long maxCacheBytes)
		{
			lock (_bitmapCache)
			{
				_maxBitmapCacheBytes = maxCacheBytes;
				TrimBitmapCache();
			}
		}

		public long GetCacheBytes()
		{
			lock (_bitmapCache)
			{
				return _bitmapCacheBytes;
			}
		}

		public bool TryGetCachedBitmap(string key, out BitmapSource? bitmap)
		{
			lock (_bitmapCache)
			{
				if (_bitmapCache.TryGetValue(key, out BitmapSource value))
				{
					bitmap = value;
					if (_bitmapCacheNodes.TryGetValue(key, out LinkedListNode<string> node))
					{
						_bitmapCacheOrder.Remove(node);
						_bitmapCacheOrder.AddFirst(node);
					}
					_cacheHits++;
					return true;
				}
				_cacheMisses++;
			}
			bitmap = null;
			return false;
		}

		public void StoreBitmap(string key, BitmapSource bitmap)
		{
			lock (_bitmapCache)
			{
				if (_bitmapCache.ContainsKey(key))
				{
					if (_bitmapCacheNodes.TryGetValue(key, out LinkedListNode<string> node))
					{
						_bitmapCacheOrder.Remove(node);
						_bitmapCacheOrder.AddFirst(node);
					}
					_bitmapCache[key] = bitmap;
				}
				else
				{
					_bitmapCache[key] = bitmap;
					LinkedListNode<string> node = _bitmapCacheOrder.AddFirst(key);
					_bitmapCacheNodes[key] = node;
					_bitmapCacheBytes += EstimateBitmapBytes(bitmap);
					TrimBitmapCache();
				}
			}
		}

		public void Clear()
		{
			lock (_bitmapCache)
			{
				_bitmapCache.Clear();
				_bitmapCacheOrder.Clear();
				_bitmapCacheNodes.Clear();
				_bitmapCacheBytes = 0L;
			}
		}

		public BitmapSource? FindAnyCachedBitmapForPage(int pageNumber, bool isThumbnail)
		{
			string prefix = isThumbnail ? $"thumb:{pageNumber}:" : $"page:{pageNumber}:";
			lock (_bitmapCache)
			{
				foreach (var kvp in _bitmapCache)
				{
					if (kvp.Key.StartsWith(prefix, StringComparison.Ordinal))
					{
						return kvp.Value;
					}
				}
			}
			return null;
		}

		private void TrimBitmapCache()
		{
			while (_bitmapCacheBytes > _maxBitmapCacheBytes && _bitmapCacheOrder.Last != null)
			{
				string key = _bitmapCacheOrder.Last.Value;
				_bitmapCacheOrder.RemoveLast();
				if (_bitmapCache.TryGetValue(key, out BitmapSource bitmap))
				{
					_bitmapCacheBytes = Math.Max(0L, _bitmapCacheBytes - EstimateBitmapBytes(bitmap));
					_bitmapCache.Remove(key);
				}
				_bitmapCacheNodes.Remove(key);
			}
		}

		private static long EstimateBitmapBytes(BitmapSource bitmap)
		{
			return (long)Math.Max(1, bitmap.PixelWidth) * (long)Math.Max(1, bitmap.PixelHeight) * 4;
		}
	}
}
