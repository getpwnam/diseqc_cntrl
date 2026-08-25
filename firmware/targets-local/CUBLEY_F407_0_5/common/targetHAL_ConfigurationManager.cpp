#include <nanoHAL.h>
#include <nanoHAL_v2.h>
#include <nanoWeak.h>

#include <string.h>

typedef struct
{
    uint8_t Count;
    HAL_Configuration_NetworkInterface *Configs[1];
} CubleyNetworkConfigurations;

typedef struct
{
    uint8_t Count;
} CubleyEmptyConfigurations;

static HAL_Configuration_NetworkInterface s_networkConfig;
static CubleyNetworkConfigurations s_networkConfigs;
static CubleyEmptyConfigurations s_wirelessConfigs;
static CubleyEmptyConfigurations s_wirelessApConfigs;
static CubleyEmptyConfigurations s_certificateStore;
static CubleyEmptyConfigurations s_deviceCertificates;

bool InitialiseNetworkDefaultConfig(HAL_Configuration_NetworkInterface *config, uint32_t configurationIndex)
{
    (void)configurationIndex;

    memset(config, 0, sizeof(HAL_Configuration_NetworkInterface));
    memcpy(config->Marker, c_MARKER_CONFIGURATION_NETWORK_V1, sizeof(c_MARKER_CONFIGURATION_NETWORK_V1));

    config->InterfaceType = NetworkInterfaceType_Ethernet;
    config->StartupAddressMode = AddressMode_DHCP;
    config->AutomaticDNS = 1;
    config->SpecificConfigId = UINT32_MAX;

    config->MacAddress[0] = 0x02;
#if defined(UID_BASE)
    const uint8_t *uniqueId = (const uint8_t *)UID_BASE;
    for (size_t index = 0; index < 11; index++)
    {
        config->MacAddress[1 + (index % 5)] ^= uniqueId[index];
    }
    config->MacAddress[5] ^= uniqueId[11];
#else
    config->MacAddress[1] = 0x43;
    config->MacAddress[2] = 0x55;
    config->MacAddress[3] = 0x42;
    config->MacAddress[4] = 0x4C;
    config->MacAddress[5] = 0x59;
#endif

    return true;
}

void ConfigurationManager_EnumerateConfigurationBlocks()
{
    InitialiseNetworkDefaultConfig(&s_networkConfig, 0);

    s_networkConfigs.Count = 1;
    s_networkConfigs.Configs[0] = &s_networkConfig;

    g_TargetConfiguration.NetworkInterfaceConfigs = (HAL_CONFIGURATION_NETWORK *)&s_networkConfigs;
    g_TargetConfiguration.Wireless80211Configs =
        (HAL_CONFIGURATION_NETWORK_WIRELESS80211 *)&s_wirelessConfigs;
    g_TargetConfiguration.WirelessAPConfigs = (HAL_CONFIGURATION_NETWORK_WIRELESSAP *)&s_wirelessApConfigs;
    g_TargetConfiguration.CertificateStore = (HAL_CONFIGURATION_X509_CERTIFICATE *)&s_certificateStore;
    g_TargetConfiguration.DeviceCertificates =
        (HAL_CONFIGURATION_X509_DEVICE_CERTIFICATE *)&s_deviceCertificates;
}

bool ConfigurationManager_GetConfigurationBlock(
    void *configurationBlock,
    DeviceConfigurationOption configuration,
    uint32_t configurationIndex)
{
    if (configuration != DeviceConfigurationOption_Network || configurationIndex != 0)
    {
        return false;
    }

    memcpy(configurationBlock, &s_networkConfig, sizeof(s_networkConfig));
    return true;
}