using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Simple.GpsCoordinates;

/// <summary>
/// GPS Coordinate with Latitude and Longitude
/// </summary>
public readonly partial struct GpsCoordinate<T> :
    IEquatable<GpsCoordinate<T>>,
    ISpanParsable<GpsCoordinate<T>>

    where T : IFloatingPointIeee754<T> {
  /// <summary>
  ///  Earth Diameter in meters taken from Azure SDK, not from NASA
  /// </summary>
  public static readonly T EarthDiameter = T.CreateSaturating(6378137.0 * 2.0);

  /// <summary>
  /// Spatial Reference Identifier for GPS coordinates
  /// </summary>
  public const int Srid = 4326;

  /// <summary>
  /// WKB type for GPS coordinates, Point
  /// </summary>
  public const int WkbType = 1;

  #region Public Properties

  /// <summary>
  /// Latitude in degrees
  /// </summary>
  public T Latitude { get; }

  /// <summary>
  /// Longitude in degrees
  /// </summary>
  public T Longitude { get; }

  #endregion Public Properties

  #region Constructors

    /// <summary>
    /// A special value to represent an invalid coordinate
    /// </summary>
    public static GpsCoordinate<T> None { get; } = new(T.NaN, T.NaN);

  /// <summary>
  /// Create a new GPS coordinate
  /// </summary>
  /// <param name="latitude">Latitude</param>
  /// <param name="longitude">Longitude</param>
  [JsonConstructor]
  public GpsCoordinate(T latitude, T longitude) {
    if (T.IsNaN(latitude) || T.IsNaN(longitude) || latitude is < -90 or > 90 || longitude is < -180 or > 180) {
      Latitude = T.NaN;
      Longitude = T.NaN;
    }
    else {
      Latitude = latitude;
      Longitude = longitude;
    }
  }

  /// <summary>
  /// Create a new GPS coordinate
  /// </summary>
  /// <param name="point">Latitude and Longitude pair</param>
  public GpsCoordinate((T latitude, T longitude) point)
      : this(point.latitude, point.longitude) {
  }

  /// <summary>
  /// Create a new GPS coordinate from a WKB (Well Known Binary)
  /// </summary>
  /// <param name="wkb">WKB (Well Known Binary)</param>
  public GpsCoordinate(byte[]? wkb) {
    if (wkb is null || wkb.Length != 21) {
      Latitude = T.NaN;
      Longitude = T.NaN;
    }
    else {
      var wktType = new byte[4];
      var longitudeBytes = new byte[8];
      var latitudeBytes = new byte[8];

      Buffer.BlockCopy(wkb, 1, wktType, 0, 4);
      Buffer.BlockCopy(wkb, 5, longitudeBytes, 0, 8);
      Buffer.BlockCopy(wkb, 13, latitudeBytes, 0, 8);

      if (BitConverter.IsLittleEndian != (wkb[0] == 1)) {
        Array.Reverse(wktType);
        Array.Reverse(longitudeBytes);
        Array.Reverse(latitudeBytes);
      }

      if (BitConverter.ToInt32(wktType) != WkbType) {
        Latitude = T.NaN;
        Longitude = T.NaN;

        return;
      }

      Longitude = T.CreateSaturating(BitConverter.ToDouble(longitudeBytes));
      Latitude = T.CreateSaturating(BitConverter.ToDouble(latitudeBytes));

      if (T.IsNaN(Latitude) || T.IsNaN(Longitude) || Latitude is < -90 or > 90 || Longitude is < -180 or > 180) {
        Latitude = T.NaN;
        Longitude = T.NaN;
      }
    }
  }

  /// <summary>
  /// Deconstruct the GPS coordinate
  /// </summary>
  /// <param name="latitude">Latitude</param>
  /// <param name="longitude">Longitude</param>
  public readonly void Deconstruct(out T latitude, out T longitude) {
    latitude = Latitude;
    longitude = Longitude;
  }

  /// <summary>
  /// Create a new GPS coordinate from a WKB (Well Known Binary)
  /// </summary>
  /// <param name="wkb">Well Known Binary</param>
  /// <returns>GPS Coordinate</returns>
  public static GpsCoordinate<T> FromWkb(byte[] wkb) => new(wkb);

  /// <summary>
  /// Create a new GPS coordinate from a WKT (Well Known Text)
  /// </summary>
  /// <param name="wkt">Well Known Text</param>
  /// <returns>GPS Coordinate</returns>
  public static GpsCoordinate<T> FromWkt(string? wkt) {
    if (string.IsNullOrWhiteSpace(wkt))
      return None;

    var match = WktRegex().Match(wkt);

    if (!match.Success)
      return None;

    if (!T.TryParse(match.Groups["long"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude) ||
        !T.TryParse(match.Groups["lat"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude))
      return None;

    return new GpsCoordinate<T>(latitude, longitude);
  }

  /// <summary>
  /// Create a new GPS coordinate from a WKT (Well Known Text)
  /// </summary>
  /// <param name="wkt">Well Known Text</param>
  /// <returns>GPS Coordinate</returns>
  public static GpsCoordinate<T> FromWkt(ReadOnlySpan<char> wkt) => FromWkt(wkt.ToString());

  #endregion Constructors

  #region Operators

  /// <summary>
  /// Implicitly convert latitude and longitude tuple to a GPS coordinate
  /// </summary>
  /// <param name="point"></param>
  public static implicit operator GpsCoordinate<T>((T latitude, T longitude) point) => new(point);

  /// <summary>
  /// Check if two GPS coordinates are equal
  /// </summary>
  /// <param name="left">Left coordinate to compare</param>
  /// <param name="right">Right coordinate</param>
  /// <returns>True if coordinates are equal</returns>
  public static bool operator ==(GpsCoordinate<T> left, GpsCoordinate<T> right) => left.Equals(right);

  /// <summary>
  /// Check if two GPS coordinates are not equal
  /// </summary>
  /// <param name="left">Left coordinate to compare</param>
  /// <param name="right">Right coordinate</param>
  /// <returns>True if coordinates are not equal</returns>
  public static bool operator !=(GpsCoordinate<T> left, GpsCoordinate<T> right) => left.Equals(right);

  #endregion Operators

  #region Public

  /// <summary>
  /// Check if the coordinate is valid
  /// </summary>
  [JsonIgnore]
  public bool IsFinite => T.IsFinite(Latitude) && T.IsFinite(Longitude);

  /// <summary>
  /// Calculate the distance between two GPS coordinates
  /// </summary>
  /// <param name="other">GPS coordinate to find distance to</param>
  /// <returns>Distance in meters</returns>
  public T DistanceTo(GpsCoordinate<T> other) {
    return !IsFinite || !other.IsFinite
        ? T.NaN
        : Distance(Latitude, Longitude, other.Latitude, other.Longitude);
  }

    /// <summary>
    /// Calculate bearing (angle) between two GPS coordinates
    /// </summary>
    /// <param name="other">GPS coordinate to find angle to</param>
    /// <returns>Angle (in radians) starting from North, counter clockwise</returns>
    public T BearingTo(GpsCoordinate<T> other)
    {
        // https://www.movable-type.co.uk/scripts/latlong.html#:~:text=const%20x%20%3D%20(%CE%BB2%2D,trigs%20%2B%202%20sqrts%20for%20haversine
        T factor = T.Pi * T.CreateSaturating(180);

      var dLambda = (Longitude - other.Longitude) * factor;
      
      var (sinLambda, cosLambda) = T.SinCos(dLambda);
      var (sinFi1, cosFi1) = T.SinCos(Latitude * factor);
      var (sinFi2, cosFi2) = T.SinCos(other.Latitude * factor);

      return T.Atan2(sinLambda * cosFi2, cosFi1 * sinFi2 - sinFi1 * cosFi2 * cosLambda);
  }

  /// <summary>
  /// Convert the GPS coordinate to a string
  /// </summary>
  /// <returns>String representation</returns>
  public readonly override string ToString() {
    return string.Join(",", Latitude.ToString("f6", CultureInfo.InvariantCulture), Longitude.ToString("f6", CultureInfo.InvariantCulture));
  }

  /// <summary>
  /// To WKT (Well Known Text) format
  /// </summary>
  /// <returns>WKT string representation</returns>
  public string ToWkt() {
    return IsFinite
      ? $"POINT ({Longitude.ToString("f6", CultureInfo.InvariantCulture)} {Latitude.ToString("f6", CultureInfo.InvariantCulture)})"
      : "POINT EMPTY";
  }

  /// <summary>
  /// To WKB (Well Known Binary) format
  /// </summary>
  /// <returns>WKB representation</returns>
  public byte[] ToWkb() {
    var buffer = new byte[21];

    buffer[0] = BitConverter.IsLittleEndian ? (byte)1 : (byte)0; // Endianness

    Buffer.BlockCopy(BitConverter.GetBytes(WkbType), 0, buffer, 1, 4); // WKB type
    Buffer.BlockCopy(BitConverter.GetBytes(double.CreateSaturating(Longitude)), 0, buffer, 5, 8);
    Buffer.BlockCopy(BitConverter.GetBytes(double.CreateSaturating(Latitude)), 0, buffer, 13, 8);

    return buffer;
  }

  #endregion Public

  #region IEquatable<GpsCoordinate>

  /// <summary>
  /// Check if two GPS coordinates are equal
  /// </summary>
  /// <param name="other">GPS coordinate to compare</param>
  /// <returns>True, if equals to other</returns>
  public readonly bool Equals(GpsCoordinate<T> other) => Latitude.Equals(other.Latitude) && Longitude.Equals(other.Longitude);

  /// <summary>
  /// Check if two GPS coordinates are equal
  /// </summary>
  /// <param name="obj">Object to compare</param>
  /// <returns>True, if equals to obj</returns>
  public readonly override bool Equals(object? obj) => obj is GpsCoordinate<T> other && Equals(other);

  /// <summary>
  /// Calculate the hash code of the GPS coordinate
  /// </summary>
  /// <returns>Hash code</returns>
  public readonly override int GetHashCode() => HashCode.Combine(Latitude, Longitude);

  #endregion IEquatable<GpsCoordinate>

  #region ISpanParsable<GpsCoordinate<T>>

  /// <summary>
  /// Parse a GPS coordinate from a string
  /// </summary>
  /// <param name="s">String to parse</param>
  /// <param name="provider">Format provider if any</param>
  /// <returns>Gps Coordinate instance</returns>
  /// <exception cref="FormatException">When string can't be parsed</exception>
  public static GpsCoordinate<T> Parse(ReadOnlySpan<char> s, IFormatProvider? provider = default) {
    if (TryParse(s, provider, out var result)) {
      return result;
    }

    throw new FormatException($"Not a valid Gps Coordinate");
  }

  /// <summary>
  /// Try to parse a GPS coordinate from a string
  /// </summary>
  /// <param name="s"></param>
  /// <param name="provider"></param>
  /// <param name="result"></param>
  /// <returns></returns>
  public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, [MaybeNullWhen(false)] out GpsCoordinate<T> result) {
    if (s.IsEmpty) {
      result = None;
      return true;
    }

    if (TryFromWkt(s, out var latitude, out var longitude)) {
      result = new GpsCoordinate<T>(latitude, longitude);

      return true;
    }

    if (TryFromWkb(s, out latitude, out longitude)) {
      result = new GpsCoordinate<T>(latitude, longitude);

      return true;
    }

    if (TryFromValue(s, out latitude, out longitude)) {
      result = new GpsCoordinate<T>(latitude, longitude);

      return true;
    }

    result = None;

    return false;
  }

  /// <summary>
  /// Parse a GPS coordinate from a string
  /// </summary>
  /// <param name="s"></param>
  /// <param name="provider"></param>
  /// <returns></returns>
  public static GpsCoordinate<T> Parse(string s, IFormatProvider? provider) {
    if (s is null) {
      return None;
    }

    return Parse(s.AsSpan(), provider);
  }

  /// <summary>
  /// Try to parse a GPS coordinate from a string
  /// </summary>
  /// <param name="s"></param>
  /// <param name="provider"></param>
  /// <param name="result"></param>
  /// <returns></returns>
  public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out GpsCoordinate<T> result) {
    if (s is null) {
      result = None;

      return true;
    }

    return TryParse(s.AsSpan(), provider, out result);
  }

  #endregion ISpanParsable<GpsCoordinate<T>>

  #region Private Methods

  private static T Distance(T latitudeA, T longitudeA, T latitudeB, T longitudeB) {
    var c180 = T.CreateSaturating(180.0);
    var c360 = T.CreateSaturating(360.0);

    var fi1 = latitudeA * T.Pi / c180;
    var fi2 = latitudeB * T.Pi / c180;

    var deltaFi = T.Sin((latitudeB - latitudeA) * T.Pi / c360);
    var deltaLambda = T.Sin((longitudeB - longitudeA) * T.Pi / c360);

    var a = deltaFi * deltaFi +
            T.Cos(fi1) * T.Cos(fi2) *
            deltaLambda * deltaLambda;

    return EarthDiameter * T.Atan2(T.Sqrt(a), T.Sqrt(T.One - a));
  }

  [GeneratedRegex(@"^\s*POINT\s*\(\s*(?<long>\S+)\s+(?<lat>\S+)\s*\)\s*$", RegexOptions.IgnoreCase, 1000)]
  private static partial Regex WktRegex();

  private static bool TryFromWkt(string wkt, out T latitude, out T longitude) {
    latitude = T.NaN;
    longitude = T.NaN;

    if ("POINT EMPTY".Equals(wkt, StringComparison.OrdinalIgnoreCase)) {
      return true;
    }

    if (string.IsNullOrWhiteSpace(wkt))
      return false;

    var match = WktRegex().Match(wkt);

    if (!match.Success)
      return false;

    if (!T.TryParse(match.Groups["long"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out longitude!) ||
        !T.TryParse(match.Groups["lat"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out latitude!)) {
      latitude = T.NaN;
      longitude = T.NaN;

      return false;
    }

    if (latitude is null || longitude is null || T.IsNaN(latitude) || T.IsNaN(longitude) || latitude is < -90 or > 90 || longitude is < -180 or > 180) {
      latitude = T.NaN;
      longitude = T.NaN;

      return false;
    }

    return true;
  }

  private static bool TryFromWkb(string wkb, out T latitude, out T longitude) {
    latitude = T.NaN;
    longitude = T.NaN;

    return false;
  }

  private static bool TryFromValue(string value, out T latitude, out T longitude) {
    latitude = T.NaN;
    longitude = T.NaN;

    return false;
  }

  private static bool TryFromWkt(ReadOnlySpan<char> wkt, out T latitude, out T longitude) {
    return TryFromWkt(wkt.ToString(), out latitude, out longitude);
  }

  private static bool TryFromWkb(ReadOnlySpan<char> wkb, out T latitude, out T longitude) {
    return TryFromWkb(wkb.ToString(), out latitude, out longitude);
  }

  private static bool TryFromValue(ReadOnlySpan<char> value, out T latitude, out T longitude) {
    return TryFromValue(value.ToString(), out latitude, out longitude);
  }

  #endregion Private Methods
}