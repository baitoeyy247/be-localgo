using AutoMapper;
using LocalGo.Application.Dtos.Responses;
using LocalGo.Domain.Entities;

namespace LocalGo.Api.Mapping;

public sealed class ReviewProfile : Profile
{
    public ReviewProfile()
    {
        CreateMap<Review, ReviewResponseDto>()
            .ForMember(dest => dest.ReviewType, opt => opt.MapFrom(src => MappingHelpers.EnumToString(src.ReviewType)));
    }
}
