﻿namespace AdSpot.Api.Mutations;

[MutationType]
public class OrderMutations
{
    [Authorize]
    //Seems like we can't use projections here
    //[UseProjection]
    [Error<InvalidListingIdError>]
    [Error<CannotOrderOwnListingError>]
    [Error<ListingPriceHasChangedError>]
    public async Task<MutationResult<Order>> OrderListing(
        int listingId,
        int userId,
        decimal price,
        string description,
        ListingRepository listingRepo,
        OrderRepository orderRepo,
        [Service] ITopicEventSender topicEventSender
    )
    {
        var listing = listingRepo.GetListingById(listingId);
        if (listing is null)
        {
            return new(new InvalidListingIdError(listingId));
        }

        if (userId == listing.UserId)
        {
            return new(new CannotOrderOwnListingError());
        }

        if (listing.Price != price)
        {
            return new(new ListingPriceHasChangedError(price, listing.Price));
        }

        var order = orderRepo.AddOrder(
            new Order
            {
                ListingId = listingId,
                UserId = userId,
                Price = price,
                Description = description
            }
        );

        // Notify seller
        var topicName = $"{listing.UserId}_{nameof(NewOrderSubscription.OnNewOrder)}";
        await topicEventSender.SendAsync(topicName, order);

        return new(order);
    }

    [Authorize]
    [Error<InvalidOrderIdError>]
    [Error<ListingDoesNotBelongToUserError>]
    public MutationResult<Order> AcceptOrder(int userId, int orderId, OrderRepository orderRepo)
    {
        var order = orderRepo.GetOrderById(orderId).Include(o => o.Listing).FirstOrDefault();

        if (order is null)
        {
            return new(new InvalidOrderIdError(orderId));
        }

        if (order.Listing.UserId != userId)
        {
            return new(new ListingDoesNotBelongToUserError(order.Listing.ListingId, userId));
        }

        var result = orderRepo.AcceptOrder(orderId);

        return new(result);
    }

    [Authorize]
    [Error<InvalidOrderIdError>]
    [Error<ListingDoesNotBelongToUserError>]
    public MutationResult<Order> RejectOrder(int userId, int orderId, OrderRepository orderRepo)
    {
        var order = orderRepo.GetOrderById(orderId).Include(o => o.Listing).FirstOrDefault();

        if (order is null)
        {
            return new(new InvalidOrderIdError(orderId));
        }

        if (order.Listing.UserId != userId)
        {
            return new(new ListingDoesNotBelongToUserError(order.Listing.ListingId, userId));
        }

        var result = orderRepo.RejectOrder(orderId);

        return new(result);
    }

    [Error<InvalidOrderIdError>]
    public MutationResult<Order> SubmitDeliverable(int orderId, string deliverable, OrderRepository orderRepo)
    {
        var order = orderRepo.GetOrderById(orderId).Include(o => o.Listing).FirstOrDefault();

        if (order is null)
        {
            return new(new InvalidOrderIdError(orderId));
        }

        order = orderRepo.SubmitDeliverable(orderId, deliverable);
        return new(order);
    }
}
