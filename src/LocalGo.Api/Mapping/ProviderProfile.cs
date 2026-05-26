using AutoMapper;
using LocalGo.Application.Dtos.Responses;
using LocalGo.Domain.Entities;

namespace LocalGo.Api.Mapping;

public sealed class ProviderProfile : Profile
{
    public ProviderProfile()
    {
        CreateMap<ProviderBranch, ProviderBranchResponseDto>();

        CreateMap<ProviderService, ProviderServiceResponseDto>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : null))
            .ForMember(
                dest => dest.BranchIds,
                opt => opt.MapFrom(src => src.BranchLinks != null
                    ? src.BranchLinks.Select(link => link.ProviderBranchId)
                    : null));

        CreateMap<Provider, ProviderResponseDto>()
            .ForMember(dest => dest.ProviderType, opt => opt.MapFrom(src => MappingHelpers.EnumToString(src.ProviderType)))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => MappingHelpers.EnumToString(src.Status)))
            .ForMember(dest => dest.Branches, opt => opt.MapFrom(src => src.Branches))
            .ForMember(dest => dest.Services, opt => opt.MapFrom(src => src.Services));
    }
}
