using System.Linq.Expressions;
using AutoMapper;
using LANCommander.SDK.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            AllowNullCollections = true;
            AllowNullDestinationValues = true;
            
            CreateMap<Data.Models.Action, SDK.Models.Action>();
            CreateMap<Data.Models.Archive, SDK.Models.Archive>();
            CreateMap<Data.Models.Company, SDK.Models.Company>();
            CreateMap<Data.Models.Collection, SDK.Models.Collection>();
            CreateMap<Data.Models.Engine, SDK.Models.Engine>();
            CreateMap<Data.Models.GameSave, SDK.Models.GameSave>();
            CreateMap<Data.Models.Genre, SDK.Models.Genre>();
            CreateMap<Data.Models.Key, SDK.Models.Key>();
            CreateMap<Data.Models.Media, SDK.Models.Media>();
            CreateMap<Data.Models.MultiplayerMode, SDK.Models.MultiplayerMode>();
            CreateMap<Data.Models.Platform, SDK.Models.Platform>();
            CreateMap<Data.Models.PlaySession, SDK.Models.PlaySession>();
            CreateMap<Data.Models.Redistributable, SDK.Models.Redistributable>();
            CreateMap<Data.Models.Server, SDK.Models.Server>();
            CreateMap<Data.Models.ServerConsole, SDK.Models.ServerConsole>();
            CreateMap<Data.Models.ServerHttpPath, SDK.Models.ServerHttpPath>();
            CreateMap<Data.Models.SavePath, SDK.Models.SavePath>();
            CreateMap<Data.Models.Script, SDK.Models.Script>();
            CreateMap<Data.Models.Tag, SDK.Models.Tag>().ReverseMap();
            CreateMap<Data.Models.User, SDK.Models.User>();
            CreateMap<Data.Models.GameCustomField, SDK.Models.GameCustomField>();
            CreateMap<Data.Models.ChatThread, SDK.Models.ChatThread>().ReverseMap();
            
            CreateEntityReferenceMap<SDK.Models.Game>(g => g.Title);

            CreateMap<Data.Models.Action, SDK.Models.Action>()
                .ForMember(dest =>
                    dest.IsPrimaryAction,
                    opt => opt.MapFrom(src => src.PrimaryAction));
            
            CreateMap<SDK.Models.Action, Data.Models.Action>()
                .ForMember(dest =>
                    dest.PrimaryAction,
                    opt => opt.MapFrom(src => src.IsPrimaryAction));

            CreateMap<Data.Models.Game, SDK.Models.Game>()
                .MaxDepth(5)
                .ForMember(dest =>
                    dest.DependentGames,
                    opt => opt.MapFrom(src => src.DependentGames.Select(d => d.Id)));

            CreateMap<Data.Models.Game, SDK.Models.DepotGame>()
                .ForMember(dest =>
                    dest.Cover,
                    opt => opt.MapFrom(src => src.Media.Where(m => m.Type == SDK.Enums.MediaType.Cover).FirstOrDefault()));

            CreateMap<Settings.Models.AuthenticationProvider, SDK.Models.AuthenticationProvider>();

            CreateMap<Data.Models.Game, EntityReference>()
                .ForMember(dest => dest.Name,
                    opt => opt.MapFrom(src => src.Title));
            
            CreateManifestMappings();
        }

        private void CreateManifestMappings()
        {
            CreateMap<Data.Models.Action, SDK.Models.Manifest.Action>()
                .ForMember(dest =>
                        dest.IsPrimaryAction,
                    opt => opt.MapFrom(src => src.PrimaryAction))
                .ReverseMap();
            
            CreateMap<Data.Models.Archive, SDK.Models.Manifest.Archive>().ReverseMap();
            CreateMap<Data.Models.Collection, SDK.Models.Manifest.Collection>().ReverseMap();
            CreateMap<Data.Models.Company, SDK.Models.Manifest.Company>().ReverseMap();
            CreateMap<Data.Models.Engine, SDK.Models.Manifest.Engine>().ReverseMap();
            CreateMap<Data.Models.Game, SDK.Models.Manifest.Game>().ReverseMap();
            CreateMap<Data.Models.GameCustomField, SDK.Models.Manifest.GameCustomField>().ReverseMap();
            CreateMap<Data.Models.Genre, SDK.Models.Manifest.Genre>().ReverseMap();
            CreateMap<Data.Models.Issue, SDK.Models.Manifest.Issue>().ReverseMap();
            CreateMap<Data.Models.Key, SDK.Models.Manifest.Key>().ReverseMap();
            CreateMap<Data.Models.Media, SDK.Models.Manifest.Media>().ReverseMap();
            CreateMap<Data.Models.MultiplayerMode, SDK.Models.Manifest.MultiplayerMode>().ReverseMap();
            CreateMap<Data.Models.Platform, SDK.Models.Manifest.Platform>().ReverseMap();
            CreateMap<Data.Models.PlaySession, SDK.Models.Manifest.PlaySession>().ReverseMap();
            CreateMap<Data.Models.Redistributable, SDK.Models.Manifest.Redistributable>().ReverseMap();
            CreateMap<Data.Models.GameSave, SDK.Models.Manifest.Save>().ReverseMap();
            CreateMap<Data.Models.SavePath, SDK.Models.Manifest.SavePath>().ReverseMap();
            CreateMap<Data.Models.Script, SDK.Models.Manifest.Script>().ReverseMap();
            CreateMap<Data.Models.Server, SDK.Models.Manifest.Server>().ReverseMap();
            CreateMap<Data.Models.ServerConsole, SDK.Models.Manifest.ServerConsole>().ReverseMap();
            CreateMap<Data.Models.ServerHttpPath, SDK.Models.Manifest.ServerHttpPath>().ReverseMap();
            CreateMap<Data.Models.Tag, SDK.Models.Manifest.Tag>().ReverseMap();
        }

        private void CreateEntityReferenceMap<TEntity>(Expression<Func<TEntity, string>> nameMember)
        {
            CreateMap<TEntity, SDK.Models.EntityReference>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(nameMember));
        }
    }
}
