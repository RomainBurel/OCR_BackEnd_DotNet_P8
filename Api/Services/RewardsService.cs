﻿using GpsUtil.Location;
using System.Collections.Concurrent;
using TourGuide.LibrairiesWrappers.Interfaces;
using TourGuide.Services.Interfaces;
using TourGuide.Users;

namespace TourGuide.Services;

public class RewardsService : IRewardsService
{
    private const double StatuteMilesPerNauticalMile = 1.15077945;
    private readonly int _defaultProximityBuffer = 10;
    private int _proximityBuffer;
    private readonly int _attractionProximityRange = 200;
    private readonly IGpsUtil _gpsUtil;
    private readonly IRewardCentral _rewardsCentral;

    public RewardsService(IGpsUtil gpsUtil, IRewardCentral rewardCentral)
    {
        _gpsUtil = gpsUtil;
        _rewardsCentral = rewardCentral;
        _proximityBuffer = _defaultProximityBuffer;
    }

    public void SetProximityBuffer(int proximityBuffer)
    {
        _proximityBuffer = proximityBuffer;
    }

    public void SetDefaultProximityBuffer()
    {
        _proximityBuffer = _defaultProximityBuffer;
    }

    public async Task CalculateRewards(User user)
    {
        user.ClearUserRewards();
        ConcurrentBag<VisitedLocation> userLocations = user.VisitedLocations;
        List<Attraction> attractions = _gpsUtil.GetAttractions();
        var newUserRewards = new ConcurrentBag<UserReward>();
        var visitedAttractionsName = new List<string>();

        var visitedAttractions = attractions.Where(a => userLocations.Any(ul => NearAttraction(ul, a)));
        var relevantLocations = userLocations.Where(ul => visitedAttractions.Any(a => NearAttraction(ul, a)));

        var visitedLocationTasks = new List<Task>();
        foreach (var visitedLocation in relevantLocations)
        {
            visitedLocationTasks.Add(Task.Run(async () =>
            {
                var nearNotVisitedAttractions = visitedAttractions.Where(a => !visitedAttractionsName.Contains(a.AttractionName) && NearAttraction(visitedLocation, a));
                var attractionTasks = new List<Task>();
                foreach (var attraction in nearNotVisitedAttractions)
                {
                    visitedAttractionsName.Add(attraction.AttractionName);
                    attractionTasks.Add(Task.Run(() =>
                    {
                        newUserRewards.Add(new UserReward(visitedLocation, attraction, GetRewardPoints(attraction, user)));
                    }));
                }

                await Task.WhenAll(attractionTasks);
            }));
        }

        await Task.WhenAll(visitedLocationTasks);

        foreach (var userReward in newUserRewards)
        {
            user.AddUserReward(userReward);
        }
    }

    public bool IsWithinAttractionProximity(Attraction attraction, Locations location)
    {
        Console.WriteLine(GetDistance(attraction, location));
        return GetDistance(attraction, location) <= _attractionProximityRange;
    }

    private bool NearAttraction(VisitedLocation visitedLocation, Attraction attraction)
    {
        return GetDistance(attraction, visitedLocation.Location) <= _proximityBuffer;
    }

    private int GetRewardPoints(Attraction attraction, User user)
    {
        return _rewardsCentral.GetAttractionRewardPoints(attraction.AttractionId, user.UserId);
    }

    public double GetDistance(Locations loc1, Locations loc2)
    {
        double lat1 = Math.PI * loc1.Latitude / 180.0;
        double lon1 = Math.PI * loc1.Longitude / 180.0;
        double lat2 = Math.PI * loc2.Latitude / 180.0;
        double lon2 = Math.PI * loc2.Longitude / 180.0;

        double angle = Math.Acos(Math.Sin(lat1) * Math.Sin(lat2)
                                + Math.Cos(lat1) * Math.Cos(lat2) * Math.Cos(lon1 - lon2));

        double nauticalMiles = 60.0 * angle * 180.0 / Math.PI;
        return StatuteMilesPerNauticalMile * nauticalMiles;
    }
}
