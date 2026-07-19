using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

public class APIController : MonoBehaviour
{
    public enum DayPhase { Sunrise, Day, Sunset, Night }

    [SerializeField] private DomeImageFader domeFader;

    // Dresden
    private const float Latitude = 51.0509f;
    private const float Longitude = 13.7383f;

    private const string Url =
        "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}&current=weather_code&daily=sunrise,sunset&timezone=Europe%2FBerlin";

    /// <summary>Halbe Breite des Sonnenauf-/-untergangs-Fensters in Minuten.</summary>
    private const int TwilightWindowMinutes = 30;

    /// <summary>Abstand zwischen zwei API-Abfragen in Sekunden.</summary>
    [SerializeField] private float fetchIntervalSeconds = 120f;

    /// <summary>Der zuletzt abgerufene WMO Weather Code (-1 = noch kein Wert).</summary>
    public int CurrentWeatherCode { get; private set; } = -1;

    /// <summary>Die zuletzt ermittelte Tagesphase.</summary>
    public DayPhase CurrentDayPhase { get; private set; } = DayPhase.Day;

    /// <summary>Wird ausgelöst, sobald ein neuer Weather Code empfangen wurde.</summary>
    public event Action<int> OnWeatherCodeReceived;

    /// <summary>Wird ausgelöst, sobald die Tagesphase ermittelt wurde.</summary>
    public event Action<DayPhase> OnDayPhaseReceived;

    void Start()
    {
        StartCoroutine(FetchLoop());
    }

    /// <summary>Fragt die API sofort und danach in festen Abständen ab.</summary>
    private IEnumerator FetchLoop()
    {
        var wait = new WaitForSeconds(fetchIntervalSeconds);

        while (true)
        {
            yield return FetchWeatherCode();
            yield return wait;
        }
    }

    public IEnumerator FetchWeatherCode()
    {
        string url = string.Format(CultureInfo.InvariantCulture, Url, Latitude, Longitude);

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Open-Meteo Anfrage fehlgeschlagen: {request.error}");
                yield break;
            }

            OpenMeteoResponse response = JsonUtility.FromJson<OpenMeteoResponse>(request.downloadHandler.text);

            if (response == null || response.current == null)
            {
                Debug.LogError("Open-Meteo Antwort konnte nicht geparst werden.");
                yield break;
            }

            CurrentWeatherCode = response.current.weather_code;
            Debug.Log($"Aktueller WMO Weather Code für Dresden: {CurrentWeatherCode}");
            OnWeatherCodeReceived?.Invoke(CurrentWeatherCode);

            if (response.daily != null &&
                response.daily.sunrise != null && response.daily.sunrise.Length > 0 &&
                response.daily.sunset != null && response.daily.sunset.Length > 0)
            {
                DetermineDayPhase(response.daily.sunrise[0], response.daily.sunset[0]);
            }
            else
            {
                Debug.LogError("Sunrise/Sunset fehlen in der Open-Meteo Antwort.");
            }

            UpdateDomeImage();
        }
    }

    /// <summary>Wählt anhand von Weather Code und Tagesphase das passende Dome-Bild aus.</summary>
    private void UpdateDomeImage()
    {
        if (domeFader == null || CurrentWeatherCode < 0) return;

        int group = MapWeatherCodeToImageGroup(CurrentWeatherCode);

        // Nur für WMO 0 und 2 gibt es 4 Bilder (morgen/tag/abend/nacht),
        // sonst nur tag/nacht: morgens -> tag, abends -> nacht
        bool hasFourPhases = group == 0 || group == 2;

        string phase = CurrentDayPhase switch
        {
            DayPhase.Sunrise => hasFourPhases ? "morgen" : "tag",
            DayPhase.Day => "tag",
            DayPhase.Sunset => hasFourPhases ? "abend" : "nacht",
            _ => "nacht"
        };

        string imageName = $"wetter_{group}_{phase}";

        if (domeFader.ShowImage(imageName))
            Debug.Log($"Dome-Bild gewechselt zu: {imageName}");
        else
            Debug.LogWarning($"Dome-Bild \"{imageName}\" nicht im DomeImageFader gefunden.");
    }

    /// <summary>Ordnet einen WMO Weather Code einer der vorhandenen Bildgruppen zu.</summary>
    private static int MapWeatherCodeToImageGroup(int code) => code switch
    {
        0 or 1 => 0,                  // klar / überwiegend klar
        2 => 2,                       // teilweise bewölkt
        3 => 3,                       // bedeckt
        45 or 48 => 45,               // Nebel
        >= 51 and <= 67 => 61,        // Niesel / Regen / gefrierender Regen
        >= 71 and <= 77 => 71,        // Schnee
        80 or 81 or 82 => 61,         // Regenschauer
        85 or 86 => 71,               // Schneeschauer
        >= 95 => 95,                  // Gewitter
        _ => 3                        // Fallback: bedeckt
    };

    private void DetermineDayPhase(string sunriseIso, string sunsetIso)
    {
        if (!DateTime.TryParse(sunriseIso, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime sunrise) ||
            !DateTime.TryParse(sunsetIso, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime sunset))
        {
            Debug.LogError($"Sunrise/Sunset konnten nicht geparst werden: '{sunriseIso}' / '{sunsetIso}'");
            return;
        }

        DateTime now = DateTime.Now;
        TimeSpan window = TimeSpan.FromMinutes(TwilightWindowMinutes);

        if (now >= sunrise - window && now <= sunrise + window)
            CurrentDayPhase = DayPhase.Sunrise;
        else if (now >= sunset - window && now <= sunset + window)
            CurrentDayPhase = DayPhase.Sunset;
        else if (now > sunrise && now < sunset)
            CurrentDayPhase = DayPhase.Day;
        else
            CurrentDayPhase = DayPhase.Night;

        string phaseText = CurrentDayPhase switch
        {
            DayPhase.Sunrise => "Sonnenaufgang",
            DayPhase.Sunset => "Sonnenuntergang",
            DayPhase.Day => "Tag",
            _ => "Nacht"
        };

        Debug.Log($"Tagesphase: {phaseText} (Sonnenaufgang: {sunrise:HH:mm}, Sonnenuntergang: {sunset:HH:mm}, jetzt: {now:HH:mm})");
        OnDayPhaseReceived?.Invoke(CurrentDayPhase);
    }

    [Serializable]
    private class OpenMeteoResponse
    {
        public CurrentData current;
        public DailyData daily;
    }

    [Serializable]
    private class CurrentData
    {
        public string time;
        public int weather_code;
    }

    [Serializable]
    private class DailyData
    {
        public string[] time;
        public string[] sunrise;
        public string[] sunset;
    }
}
