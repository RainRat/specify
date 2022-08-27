use std::collections::HashMap;

use wmi::*;

type WMIMap = HashMap<String, Variant>;
type WMIVec = Vec<WMIMap>;
type DumbResult<T> = Result<T, Box<dyn std::error::Error>>;

/**
 * Makes a WMI connection, using the default namespace (probably ROOT).
 *This exists to prevent typing it out all the time.
 */
fn get_wmi_con() -> DumbResult<WMIConnection> {
    let com_con = wmi::COMLibrary::new()?;
    let wmi_con = WMIConnection::new(com_con.into())?;
    return Ok(wmi_con);
}

/**
 * Makes a WMI connection, using a specified namespace.
 * This exists to prevent typing it out all the time.
 */
fn get_wmi_con_namespace(namespace: &str) -> DumbResult<WMIConnection> {
    let com_con = wmi::COMLibrary::new()?;
    let wmi_con = WMIConnection::with_namespace_path(namespace, com_con.into())?;
    return Ok(wmi_con);
}

pub fn get_cimos() -> DumbResult<WMIMap> {
    let wmi_con = get_wmi_con()?;

    let results: WMIVec = wmi_con.raw_query("SELECT * FROM Win32_OperatingSystem")?;
    
    let mut this_one: WMIMap = HashMap::new();
    // We know that there will be only one at most
    for os in results {
        this_one = os;
    }


    Ok(this_one)
}

/**
 * Get the AV/Firewall information, in a tuple. AV is first, firewall is second.
 * If Windows Defender is used as a firewall, it will be blank. This is because of the WMI value.
 */
pub fn get_avfw() -> DumbResult<(WMIVec, WMIVec)> {
    let wmi_con = get_wmi_con_namespace(r"ROOT\SECURITYCENTER2")?;
    let av_results: Vec<WMIMap> = wmi_con.raw_query("SELECT * FROM AntivirusProduct")?;
    let fw_results: Vec<WMIMap> = wmi_con.raw_query("SELECT * FROM FirewallProduct")?;

    Ok((av_results, fw_results))
}
