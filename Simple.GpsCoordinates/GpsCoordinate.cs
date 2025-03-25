using System.Globalization;
using System.Numerics;
using System.Text.Json.Serialization;

namespace Simple.GpsCoordinates;

/// <summary>
/// GPS Coordinate with Latitude and Longitude
/// </summary>
public readonly struct GpsCoordinate<T> : IEquatable<GpsCoordinate<T>> where T : IFloatingPointIeee754<T> {
  /// <summary>
  ///  Earth Diameter in meters taken from Azure SDK, not from NASA
  /// </summary>
  public static readonly T EarthDiameter = T.CreateSaturating(6378137 * 2.0);

  /// <summary>
  /// Spatial Reference Identifier for GPS coordinates
  /// </summary>
  public const int Srid = 4326;

  /// <summary>
  /// WKB type for GPS coordinates, Point
  /// </summary>
  public const int WkbType = 1;

  #region Private Fields

  /// <summary>
  /// Latitude in degrees
  /// </summary>
  public T Latitude { get; }

  /// <summary>
  /// Longitude in degrees
  /// </summary>
  public T Longitude { get; }

  #endregion Private Fields

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
  public readonly bool IsFinite => T.IsFinite(Latitude) && T.IsFinite(Longitude);

  /// <summary>
  /// Calculate the distance between two GPS coordinates
  /// </summary>
  /// <param name="other">GPS coordinate to find distance to</param>
  /// <returns>Distance in meters</returns>
  public readonly T DistanceTo(GpsCoordinate<T> other) {
    return !IsFinite || !other.IsFinite
        ? T.NaN
        : Distance(Latitude, Longitude, other.Latitude, other.Longitude);
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
    return $"POINT ({Longitude.ToString("f6", CultureInfo.InvariantCulture)} {Latitude.ToString("f6", CultureInfo.InvariantCulture)})";
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

  #endregion Private Methods
}