#include <nanoHAL.h>
#include <nanoHAL_v2.h>
#include <string.h>

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