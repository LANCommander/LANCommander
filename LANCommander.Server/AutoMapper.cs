using AutoMapper;

namespace LANCommander.Server
{
    public class AutoMapper : Profile
    {
        public AutoMapper()
        {
            CreateMap<Data.Models.Action, SDK.Models.Action>();
            CreateMap<Data.Models.Archive, SDK.Models.Archive>();
            CreateMap<Data.Models.Company, SDK.Models.Company>();
            CreateMap<Data.Models.Collection, SDK.Models.Collection>();
            CreateMap<Data.Models.Engine, SDK.Models.Engine>();
            CreateMap<Data.Models.Game, SDK.Models.Game>();
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
            CreateMap<Data.Models.Tag, SDK.Models.Tag>();
            CreateMap<Data.Models.User, SDK.Models.User>();

            CreateMap<Services.Models.Settings, SDK.Models.Settings>()
                .ForMember(dest =>
                    dest.IPXRelayHost,
                    opt => opt.MapFrom(src => src.IPXRelay.Host))
                .ForMember(dest =>
                    dest.IPXRelayPort,
                    opt => opt.MapFrom(src => src.IPXRelay.Port));

            CreateMap<Data.Models.Action, SDK.Models.Action>()
                .ForMember(dest =>
                    dest.IsPrimaryAction,
                    opt => opt.MapFrom(src => src.PrimaryAction));

            CreateMap<Data.Models.Game, SDK.Models.DepotGame>()
                .ForMember(dest =>
                    dest.Cover,
                    opt => opt.MapFrom(src => src.Media.Where(m => m.Type == SDK.Enums.MediaType.Cover).FirstOrDefault()));
        }
    }
}
