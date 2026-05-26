using AutoMapper;
using LocalGo.Application.Dtos.Responses;
using LocalGo.Domain.Entities;

namespace LocalGo.Api.Mapping;

public sealed class AppointmentProfile : Profile
{
    public AppointmentProfile()
    {
        CreateMap<Appointment, AppointmentResponseDto>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => MappingHelpers.EnumToString(src.Status)))
            .ForMember(dest => dest.Latitude, opt => opt.MapFrom(src => MappingHelpers.GetCoordinates(src.Location).Latitude))
            .ForMember(dest => dest.Longitude, opt => opt.MapFrom(src => MappingHelpers.GetCoordinates(src.Location).Longitude));
    }
}
