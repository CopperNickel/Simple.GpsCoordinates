using System.Globalization;
using System.Text.Json.Serialization;

namespace Simple.GpsCoordinates;

public readonly struct GpsCoordinate : IEquatable<GpsCoordinate> {
  /// <summary>
  ///  Earth Diameter in meters taken from Azure SDK, not from NASA
  /// </summary>
  public const double EarthDiameter = 6378137 * 2.0;

  #region Private Fields

  /// <summary>
  /// Latitude in degrees
  /// </summary>
  public double Latitude { get; }

  /// <summary>
  /// Longitude in degrees
  /// </summary>
  public double Longitude { get; }

  #endregion Private Fields

  #region Constructors

  /// <summary>
  /// A special value to represent an invalid coordinate
  /// </summary>
  public static GpsCoordinate None { get; } = new(double.NaN, double.NaN);

  /// <summary>
  /// Create a new GPS coordinate
  /// </summary>
  /// <param name="latitude">Latitude</param>
  /// <param name="longitude">Longitude</param>
  [JsonConstructor]
  public GpsCoordinate(double latitude, double longitude) {
    if (double.IsNaN(latitude) || double.IsNaN(longitude) || latitude is < -90 or > 90 ||
        longitude is < -180 or > 180) {
      Latitude = double.NaN;
      Longitude = double.NaN;
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
  public GpsCoordinate((double latitude, double longitude) point)
      : this(point.latitude, point.longitude) {
  }

  /// <summary>
  /// Deconstruct the GPS coordinate
  /// </summary>
  /// <param name="latitude">Latitude</param>
  /// <param name="longitude">Longitude</param>
  public readonly void Deconstruct(out double latitude, out double longitude) {
    latitude = Latitude;
    longitude = Longitude;
  }

  #endregion Constructors

  #region Operators

  /// <summary>
  /// Implicitly convert latitude and longitude tuple to a GPS coordinate
  /// </summary>
  /// <param name="point"></param>
  public static implicit operator GpsCoordinate((double latitude, double longitude) point) => new GpsCoordinate(point);

  #endregion Operators

  #region Public

  /// <summary>
  /// Check if the coordinate is valid
  /// </summary>
  [JsonIgnore] 
  public readonly bool IsFinite => double.IsFinite(Latitude) && double.IsFinite(Longitude);

  /// <summary>
  /// Calculate the distance between two GPS coordinates
  /// </summary>
  /// <param name="other">GPS coordinate to find distance to</param>
  /// <returns>Distance in meters</returns>
  public readonly double DistanceTo(GpsCoordinate? other) {
    return other is null || !IsFinite || !other.IsFinite
        ? double.NaN
        : Distance(Latitude, Longitude, other.Latitude, other.Longitude);
  }

  /// <summary>
  /// Convert the GPS coordinate to a string
  /// </summary>
  /// <returns>String representation</returns>
  public readonly override string ToString() {
    return string.Join(",", Latitude.ToString("f6", CultureInfo.InvariantCulture), Longitude.ToString("f6", CultureInfo.InvariantCulture));
  }

  #endregion Public

  #region IEquatable<GpsCoordinate>

  /// <summary>
  /// Check if two GPS coordinates are equal
  /// </summary>
  /// <param name="other">GPS coordinate to compare</param>
  /// <returns>True, if equals to other</returns>
  public readonly bool Equals(GpsCoordinate other) => Latitude.Equals(other.Latitude) && Longitude.Equals(other.Longitude);

  /// <summary>
  /// Check if two GPS coordinates are equal
  /// </summary>
  /// <param name="obj">Object to compare</param>
  /// <returns>True, if equals to obj</returns>
  public readonly override bool Equals(object? obj) => obj is GpsCoordinate other && Equals(other);

  /// <summary>
  /// Calculate the hash code of the GPS coordinate
  /// </summary>
  /// <returns>Hash code</returns>
  public readonly override int GetHashCode() => HashCode.Combine(Latitude, Longitude);

  #endregion IEquatable<GpsCoordinate>

  #region Private Methods

  private static double Distance(double latitudeA, double longitudeA, double latitudeB, double longitudeB) {
    var fi1 = latitudeA * Math.PI / 180.0;
    var fi2 = latitudeB * Math.PI / 180.0;
    var deltaFi = Math.Sin((latitudeB - latitudeA) * Math.PI / 360.0);
    var deltaLambda = Math.Sin((longitudeB - longitudeA) * Math.PI / 360.0);

    var a = deltaFi * deltaFi +
            Math.Cos(fi1) * Math.Cos(fi2) *
            deltaLambda * deltaLambda;

    return EarthDiameter * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
  }

  #endregion Private Methods
}