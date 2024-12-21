using Application.UseCases.Projections.Regions;
using AutoMapper;
using Domain.Aggregates.Regions;

namespace Application.UseCases.Regions.Queries.List.Districts;

public class ListDistrictMapping : Profile
{
    public ListDistrictMapping()
    {
        CreateMap<District, DistrictProjection>();
        CreateMap<District, DistrictDetailProjection>();
    }
}
