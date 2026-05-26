using AutoMapper;
using LocalGo.Application.Dtos.Responses;
using LocalGo.Domain.Entities;

namespace LocalGo.Api.Mapping;

public sealed class BidProfile : Profile
{
    public BidProfile()
    {
        CreateMap<Bid, BidResponseDto>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => MappingHelpers.EnumToString(src.Status)));

        CreateMap<Bid, ServiceRequestBidSummaryResponseDto>()
            .ForMember(dest => dest.ProviderName, opt => opt.MapFrom(src => src.Provider != null ? src.Provider.Name : null))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => MappingHelpers.EnumToString(src.Status)));
    }
}
