using AutoMapper;
using LocalGo.Application.Dtos.Responses;
using LocalGo.Domain.Entities;

namespace LocalGo.Api.Mapping;

public sealed class ServiceRequestProfile : Profile
{
    public ServiceRequestProfile()
    {
        CreateMap<ServiceRequest, ServiceRequestResponseDto>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : null))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => MappingHelpers.EnumToString(src.Status)))
            .ForMember(dest => dest.Source, opt => opt.MapFrom(src => MappingHelpers.EnumToString(src.Source)))
            .ForMember(dest => dest.Bids, opt => opt.MapFrom(src => src.Bids));
    }
}
