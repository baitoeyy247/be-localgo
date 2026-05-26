using LocalGo.Application.Abstractions.Notifications;
using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Application.Common;
using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;
using LocalGo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LocalGo.Tests;

/// <summary>
/// In-memory DB seeding for marketplace / appointment flow tests (see also scripts/smoke-appointment-flow.sh).
/// </summary>
internal static class TestDbFactory
{
    public static LocalGoDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LocalGoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new LocalGoDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    public static INotificationPublisher NoopNotifications()
    {
        var notifications = Substitute.For<INotificationPublisher>();
        notifications
            .EnqueueAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return notifications;
    }

    public static async Task<SeededMarketplaceJob> SeedMarketplaceJobAsync(
        LocalGoDbContext db,
        bool withPendingAppointment = false)
    {
        var now = DateTime.UtcNow;
        var requester = new User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"line-req-{Guid.NewGuid():N}",
            DisplayName = "Test Requester",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var providerUser = new User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"line-prov-{Guid.NewGuid():N}",
            DisplayName = "Test Provider",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var category = new ServiceCategory
        {
            Id = Guid.NewGuid(),
            Name = "Test Category",
            Slug = $"test-{Guid.NewGuid():N}",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var provider = new Provider
        {
            Id = Guid.NewGuid(),
            OwnerUserId = providerUser.Id,
            ProviderType = ProviderType.Individual,
            Name = "Test Provider Co",
            Status = ProviderStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var branch = new ProviderBranch
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            Name = "Main",
            Latitude = 13.7563,
            Longitude = 100.5018,
            Location = GeoHelper.CreatePoint(13.7563, 100.5018),
            ServiceRadiusMeters = 8000,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var service = new ProviderService
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            CategoryId = category.Id,
            Title = "Test Service",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var request = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            RequesterUserId = requester.Id,
            CategoryId = category.Id,
            Title = "Marketplace job",
            Latitude = 13.7563,
            Longitude = 100.5018,
            Location = GeoHelper.CreatePoint(13.7563, 100.5018),
            SearchRadiusMeters = 8000,
            Status = ServiceRequestStatus.Scheduled,
            Source = ServiceRequestSource.Marketplace,
            SelectedProviderId = provider.Id,
            LastProgressAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var bid = new Bid
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = request.Id,
            ProviderId = provider.Id,
            PriceText = "500",
            Status = BidStatus.Selected,
            CreatedAt = now,
            UpdatedAt = now,
        };
        request.SelectedBidId = bid.Id;

        db.Users.AddRange(requester, providerUser);
        db.ServiceCategories.Add(category);
        db.Providers.Add(provider);
        db.ProviderBranches.Add(branch);
        db.ProviderServices.Add(service);
        db.ProviderServiceBranches.Add(new ProviderServiceBranch
        {
            Id = Guid.NewGuid(),
            ProviderServiceId = service.Id,
            ProviderBranchId = branch.Id,
            IsActive = true,
            CreatedAt = now,
        });
        db.ServiceRequests.Add(request);
        db.Bids.Add(bid);

        Appointment? appointment = null;
        if (withPendingAppointment)
        {
            appointment = new Appointment
            {
                Id = Guid.NewGuid(),
                ServiceRequestId = request.Id,
                ProviderId = provider.Id,
                RequesterUserId = requester.Id,
                ScheduledAt = now.AddDays(2),
                AddressText = "Test address",
                Status = AppointmentStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Appointments.Add(appointment);
        }

        await db.SaveChangesAsync();
        return new SeededMarketplaceJob(requester, providerUser, provider, request, bid, appointment);
    }

    public static async Task<SeededProviderSelectedBid> SeedProviderSelectedBidAsync(LocalGoDbContext db)
    {
        var now = DateTime.UtcNow;
        var requester = new User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"line-req-sel-{Guid.NewGuid():N}",
            DisplayName = "Selected Requester",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var providerUser = new User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"line-prov-sel-{Guid.NewGuid():N}",
            DisplayName = "Selected Provider",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var otherProviderUser = new User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"line-prov-other-{Guid.NewGuid():N}",
            DisplayName = "Other Provider",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var category = new ServiceCategory
        {
            Id = Guid.NewGuid(),
            Name = "Sel Cat",
            Slug = $"sel-{Guid.NewGuid():N}",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var provider = new Provider
        {
            Id = Guid.NewGuid(),
            OwnerUserId = providerUser.Id,
            ProviderType = ProviderType.Individual,
            Name = "Selected Co",
            Status = ProviderStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var otherProvider = new Provider
        {
            Id = Guid.NewGuid(),
            OwnerUserId = otherProviderUser.Id,
            ProviderType = ProviderType.Individual,
            Name = "Other Co",
            Status = ProviderStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var request = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            RequesterUserId = requester.Id,
            CategoryId = category.Id,
            Title = "Selected bid job",
            Latitude = 13.7563,
            Longitude = 100.5018,
            Location = GeoHelper.CreatePoint(13.7563, 100.5018),
            SearchRadiusMeters = 8000,
            Status = ServiceRequestStatus.ProviderSelected,
            Source = ServiceRequestSource.Marketplace,
            SelectedProviderId = provider.Id,
            LastProgressAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var selectedBid = new Bid
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = request.Id,
            ProviderId = provider.Id,
            PriceText = "500",
            Status = BidStatus.Selected,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var rejectedBid = new Bid
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = request.Id,
            ProviderId = otherProvider.Id,
            PriceText = "600",
            Status = BidStatus.Rejected,
            CreatedAt = now,
            UpdatedAt = now,
        };
        request.SelectedBidId = selectedBid.Id;

        db.Users.AddRange(requester, providerUser, otherProviderUser);
        db.ServiceCategories.Add(category);
        db.Providers.AddRange(provider, otherProvider);
        db.ServiceRequests.Add(request);
        db.Bids.AddRange(selectedBid, rejectedBid);
        await db.SaveChangesAsync();

        return new SeededProviderSelectedBid(requester, providerUser, provider, request, selectedBid, rejectedBid);
    }

    public static async Task<SeededDirectBooking> SeedDirectBookingAsync(LocalGoDbContext db)
    {
        var now = DateTime.UtcNow;
        var requester = new User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"line-req-db-{Guid.NewGuid():N}",
            DisplayName = "Direct Requester",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var providerUser = new User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"line-prov-db-{Guid.NewGuid():N}",
            DisplayName = "Direct Provider",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var category = new ServiceCategory
        {
            Id = Guid.NewGuid(),
            Name = "Direct Cat",
            Slug = $"direct-{Guid.NewGuid():N}",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var provider = new Provider
        {
            Id = Guid.NewGuid(),
            OwnerUserId = providerUser.Id,
            ProviderType = ProviderType.Individual,
            Name = "Direct Provider Co",
            Status = ProviderStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var service = new ProviderService
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            CategoryId = category.Id,
            Title = "Direct Service",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var request = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            RequesterUserId = requester.Id,
            CategoryId = category.Id,
            Title = "Direct booking job",
            Latitude = 13.7563,
            Longitude = 100.5018,
            Location = GeoHelper.CreatePoint(13.7563, 100.5018),
            SearchRadiusMeters = 5000,
            Status = ServiceRequestStatus.ProviderSelected,
            Source = ServiceRequestSource.DirectBooking,
            SelectedProviderId = provider.Id,
            ProviderServiceId = service.Id,
            LastProgressAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = request.Id,
            ProviderId = provider.Id,
            RequesterUserId = requester.Id,
            ScheduledAt = now.AddDays(1),
            AddressText = "Direct address",
            Status = AppointmentStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Users.AddRange(requester, providerUser);
        db.ServiceCategories.Add(category);
        db.Providers.Add(provider);
        db.ProviderServices.Add(service);
        db.ServiceRequests.Add(request);
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        return new SeededDirectBooking(requester, providerUser, provider, request, appointment);
    }

    internal sealed record SeededMarketplaceJob(
        User Requester,
        User ProviderUser,
        Provider Provider,
        ServiceRequest Request,
        Bid Bid,
        Appointment? Appointment);

    internal sealed record SeededProviderSelectedBid(
        User Requester,
        User ProviderUser,
        Provider Provider,
        ServiceRequest Request,
        Bid Bid,
        Bid RejectedBid);

    internal sealed record SeededDirectBooking(
        User Requester,
        User ProviderUser,
        Provider Provider,
        ServiceRequest Request,
        Appointment Appointment);

    /// <summary>
    /// Requester + in-range matching provider (with service-branch link), far provider, wrong-category provider.
    /// </summary>
    public static async Task<SeededOpenMatchingNotification> SeedOpenMatchingNotificationAsync(
        LocalGoDbContext db,
        bool matchingProviderWithoutBranchLinks = false)
    {
        var now = DateTime.UtcNow;
        const double requestLat = 13.7563;
        const double requestLng = 100.5018;
        const int searchRadiusMeters = 5000;

        var requester = new User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"line-req-open-{Guid.NewGuid():N}",
            DisplayName = "Open Requester",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var matchingProviderUser = new User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"line-prov-match-{Guid.NewGuid():N}",
            DisplayName = "Matching Provider",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var farProviderUser = new User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"line-prov-far-{Guid.NewGuid():N}",
            DisplayName = "Far Provider",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var wrongCategoryProviderUser = new User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"line-prov-wrong-{Guid.NewGuid():N}",
            DisplayName = "Wrong Category Provider",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var category = new ServiceCategory
        {
            Id = Guid.NewGuid(),
            Name = "Plumbing",
            Slug = $"plumb-{Guid.NewGuid():N}",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var otherCategory = new ServiceCategory
        {
            Id = Guid.NewGuid(),
            Name = "Electrical",
            Slug = $"elec-{Guid.NewGuid():N}",
            SortOrder = 2,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var matchingProvider = new Provider
        {
            Id = Guid.NewGuid(),
            OwnerUserId = matchingProviderUser.Id,
            ProviderType = ProviderType.Individual,
            Name = "Nearby Co",
            Status = ProviderStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var farProvider = new Provider
        {
            Id = Guid.NewGuid(),
            OwnerUserId = farProviderUser.Id,
            ProviderType = ProviderType.Individual,
            Name = "Far Co",
            Status = ProviderStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var wrongCategoryProvider = new Provider
        {
            Id = Guid.NewGuid(),
            OwnerUserId = wrongCategoryProviderUser.Id,
            ProviderType = ProviderType.Individual,
            Name = "Other Cat Co",
            Status = ProviderStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var matchingBranch = new ProviderBranch
        {
            Id = Guid.NewGuid(),
            ProviderId = matchingProvider.Id,
            Name = "Nearby",
            Latitude = requestLat,
            Longitude = requestLng,
            Location = GeoHelper.CreatePoint(requestLat, requestLng),
            ServiceRadiusMeters = searchRadiusMeters,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var farBranch = new ProviderBranch
        {
            Id = Guid.NewGuid(),
            ProviderId = farProvider.Id,
            Name = "Chiang Mai",
            Latitude = 18.7883,
            Longitude = 98.9853,
            Location = GeoHelper.CreatePoint(18.7883, 98.9853),
            ServiceRadiusMeters = 3000,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var wrongCategoryBranch = new ProviderBranch
        {
            Id = Guid.NewGuid(),
            ProviderId = wrongCategoryProvider.Id,
            Name = "Wrong cat branch",
            Latitude = requestLat,
            Longitude = requestLng,
            Location = GeoHelper.CreatePoint(requestLat, requestLng),
            ServiceRadiusMeters = searchRadiusMeters,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var matchingService = new ProviderService
        {
            Id = Guid.NewGuid(),
            ProviderId = matchingProvider.Id,
            CategoryId = category.Id,
            Title = "Nearby plumbing",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var farService = new ProviderService
        {
            Id = Guid.NewGuid(),
            ProviderId = farProvider.Id,
            CategoryId = category.Id,
            Title = "Far plumbing",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var wrongCategoryService = new ProviderService
        {
            Id = Guid.NewGuid(),
            ProviderId = wrongCategoryProvider.Id,
            CategoryId = otherCategory.Id,
            Title = "Electrical only",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Users.AddRange(requester, matchingProviderUser, farProviderUser, wrongCategoryProviderUser);
        db.ServiceCategories.AddRange(category, otherCategory);
        db.Providers.AddRange(matchingProvider, farProvider, wrongCategoryProvider);
        db.ProviderBranches.AddRange(matchingBranch, farBranch, wrongCategoryBranch);
        db.ProviderServices.AddRange(matchingService, farService, wrongCategoryService);

        if (!matchingProviderWithoutBranchLinks)
        {
            db.ProviderServiceBranches.AddRange(
                new ProviderServiceBranch
                {
                    Id = Guid.NewGuid(),
                    ProviderServiceId = matchingService.Id,
                    ProviderBranchId = matchingBranch.Id,
                    IsActive = true,
                    CreatedAt = now,
                },
                new ProviderServiceBranch
                {
                    Id = Guid.NewGuid(),
                    ProviderServiceId = farService.Id,
                    ProviderBranchId = farBranch.Id,
                    IsActive = true,
                    CreatedAt = now,
                },
                new ProviderServiceBranch
                {
                    Id = Guid.NewGuid(),
                    ProviderServiceId = wrongCategoryService.Id,
                    ProviderBranchId = wrongCategoryBranch.Id,
                    IsActive = true,
                    CreatedAt = now,
                });
        }

        await db.SaveChangesAsync();

        return new SeededOpenMatchingNotification(
            requester,
            matchingProviderUser,
            farProviderUser,
            wrongCategoryProviderUser,
            category,
            matchingProvider,
            farProvider,
            wrongCategoryProvider,
            searchRadiusMeters,
            requestLat,
            requestLng);
    }

    /// <summary>Open marketplace request with one provider ready to bid.</summary>
    public static async Task<SeededOpenBidRequest> SeedOpenBidRequestAsync(LocalGoDbContext db)
    {
        var now = DateTime.UtcNow;
        var requester = new User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"line-req-bid-{Guid.NewGuid():N}",
            DisplayName = "Bid Requester",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var providerUser = new User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"line-prov-bid-{Guid.NewGuid():N}",
            DisplayName = "Bid Provider",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var category = new ServiceCategory
        {
            Id = Guid.NewGuid(),
            Name = "Bid Cat",
            Slug = $"bid-{Guid.NewGuid():N}",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var provider = new Provider
        {
            Id = Guid.NewGuid(),
            OwnerUserId = providerUser.Id,
            ProviderType = ProviderType.Individual,
            Name = "Bid Provider Co",
            Status = ProviderStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var request = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            RequesterUserId = requester.Id,
            CategoryId = category.Id,
            Title = "Open for bids",
            Latitude = 13.7563,
            Longitude = 100.5018,
            Location = GeoHelper.CreatePoint(13.7563, 100.5018),
            SearchRadiusMeters = 5000,
            Status = ServiceRequestStatus.Open,
            Source = ServiceRequestSource.Marketplace,
            LastProgressAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Users.AddRange(requester, providerUser);
        db.ServiceCategories.Add(category);
        db.Providers.Add(provider);
        db.ServiceRequests.Add(request);
        await db.SaveChangesAsync();

        return new SeededOpenBidRequest(requester, providerUser, provider, request, category);
    }

    internal sealed record SeededOpenBidRequest(
        User Requester,
        User ProviderUser,
        Provider Provider,
        ServiceRequest Request,
        ServiceCategory Category);

    internal sealed record SeededOpenMatchingNotification(
        User Requester,
        User MatchingProviderUser,
        User FarProviderUser,
        User WrongCategoryProviderUser,
        ServiceCategory Category,
        Provider MatchingProvider,
        Provider FarProvider,
        Provider WrongCategoryProvider,
        int SearchRadiusMeters,
        double RequestLatitude,
        double RequestLongitude);
}
