using GpsUtil.Location;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using TourGuide.LibrairiesWrappers.Interfaces;
using TourGuide.Services.Interfaces;
using TourGuide.Users;
using TourGuide.Utilities;
using TripPricer;

namespace TourGuide.Services;

public class TourGuideService : ITourGuideService
{
    private readonly ILogger _logger;
    private readonly IGpsUtil _gpsUtil;
    private readonly IRewardsService _rewardsService;
    private readonly IRewardCentral _rewardCentral;
    private readonly TripPricer.TripPricer _tripPricer;
    public Tracker Tracker { get; private set; }
    private readonly Dictionary<string, User> _internalUserMap = new();
    private const string TripPricerApiKey = "test-server-api-key";
    private bool _testMode = true;

    public TourGuideService(ILogger<TourGuideService> logger, IGpsUtil gpsUtil, IRewardsService rewardsService, IRewardCentral rewardCentral, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _tripPricer = new();
        _gpsUtil = gpsUtil;
        _rewardsService = rewardsService;
        _rewardCentral = rewardCentral;

        CultureInfo.CurrentCulture = new CultureInfo("en-US");

        if (_testMode)
        {
            _logger.LogInformation("TestMode enabled");
            _logger.LogDebug("Initializing users");
            InitializeInternalUsers();
            _logger.LogDebug("Finished initializing users");
        }

        var trackerLogger = loggerFactory.CreateLogger<Tracker>();

        Tracker = new Tracker(this, trackerLogger);
        AddShutDownHook();
    }

    public List<UserReward> GetUserRewards(User user)
    {
        return user.UserRewards;
    }

    public VisitedLocation GetUserLocation(User user)
    {
        return user.VisitedLocations.Any() ? user.GetLastVisitedLocation() : TrackUserLocation(user);
    }

    public User GetUser(string userName)
    {
        return _internalUserMap.ContainsKey(userName) ? _internalUserMap[userName] : null;
    }

    public List<User> GetAllUsers()
    {
        return _internalUserMap.Values.ToList();
    }

    public void AddUser(User user)
    {
        if (!_internalUserMap.ContainsKey(user.UserName))
        {
            _internalUserMap.Add(user.UserName, user);
        }
    }

    public List<Provider> GetTripDeals(User user)
    {
        int cumulativeRewardPoints = user.UserRewards.Sum(i => i.RewardPoints);
        List<Provider> providers = _tripPricer.GetPrice(TripPricerApiKey, user.UserId,
            user.UserPreferences.NumberOfAdults, user.UserPreferences.NumberOfChildren,
            user.UserPreferences.TripDuration, cumulativeRewardPoints);
        user.TripDeals = providers;
        return providers;
    }

    public VisitedLocation TrackUserLocation(User user)
    {
        VisitedLocation visitedLocation = _gpsUtil.GetUserLocation(user.UserId);
        user.AddToVisitedLocations(visitedLocation);
        _rewardsService.CalculateRewards(user);
        return visitedLocation;
    }

    public JsonArray GetNearByAttractions(VisitedLocation visitedLocation)
    {
        var allAttractions = _gpsUtil.GetAttractions();
        if (allAttractions.Count > 5)
        {
            allAttractions = allAttractions.OrderBy(a => _rewardsService.GetDistance(a, visitedLocation.Location)).Take(5).ToList();
        }
        var result = new JsonArray();
        allAttractions.ForEach(a =>
        {
            var attractionNode = new JsonObject();
            attractionNode.Add("AttractionName", a.AttractionName);
            attractionNode.Add("AttractionLatitude", a.Latitude);
            attractionNode.Add("AttractionLongitude", a.Longitude);
            attractionNode.Add("UserLocationLatitude", visitedLocation.Location.Latitude);
            attractionNode.Add("UserLocationLongitude", visitedLocation.Location.Longitude);
            attractionNode.Add("Distance", _rewardsService.GetDistance(a, visitedLocation.Location));
            attractionNode.Add("Rewards", _rewardCentral.GetAttractionRewardPoints(a.AttractionId, visitedLocation.UserId));
            result.Add(attractionNode);
        });

        return result;
    }

    private void AddShutDownHook()
    {
        AppDomain.CurrentDomain.ProcessExit += (sender, e) => Tracker.StopTracking();
    }

    /**********************************************************************************
    * 
    * Methods Below: For Internal Testing
    * 
    **********************************************************************************/

    private void InitializeInternalUsers()
    {
        for (int i = 0; i < InternalTestHelper.GetInternalUserNumber(); i++)
        {
            var userName = $"internalUser{i}";
            var user = new User(Guid.NewGuid(), userName, "000", $"{userName}@tourGuide.com");
            GenerateUserLocationHistory(user);
            _internalUserMap.Add(userName, user);
        }

        _logger.LogDebug($"Created {InternalTestHelper.GetInternalUserNumber()} internal test users.");
    }

    private void GenerateUserLocationHistory(User user)
    {
        for (int i = 0; i < 3; i++)
        {
            var visitedLocation = new VisitedLocation(user.UserId, new Locations(GenerateRandomLatitude(), GenerateRandomLongitude()), GetRandomTime());
            user.AddToVisitedLocations(visitedLocation);
        }
    }

    private static readonly Random random = new Random();

    private double GenerateRandomLongitude()
    {
        return new Random().NextDouble() * (180 - (-180)) + (-180);
    }

    private double GenerateRandomLatitude()
    {
        return new Random().NextDouble() * (90 - (-90)) + (-90);
    }

    private DateTime GetRandomTime()
    {
        return DateTime.UtcNow.AddDays(-new Random().Next(30));
    }
}
